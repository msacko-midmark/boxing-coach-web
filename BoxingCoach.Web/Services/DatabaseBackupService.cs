using Microsoft.Data.Sqlite;

namespace BoxingCoach.Web.Services;

/// <summary>
/// Describes a SQLite database backup that is available to administrators.
/// </summary>
/// <param name="FileName">
/// The leaf file name used to identify the backup without exposing or accepting an arbitrary path.
/// </param>
/// <param name="CreatedAtUtc">The UTC creation time reported by the backup file.</param>
/// <param name="SizeBytes">The physical size of the backup file in bytes.</param>
/// <param name="IsPreRestoreSnapshot">
/// <see langword="true"/> when the backup was captured automatically immediately before a restore;
/// otherwise, <see langword="false"/> for an administrator-requested backup.
/// </param>
public sealed record DatabaseBackup(
    string FileName,
    DateTime CreatedAtUtc,
    long SizeBytes,
    bool IsPreRestoreSnapshot);

/// <summary>
/// Defines administrative operations for enumerating, creating, and restoring database backups.
/// </summary>
/// <remarks>
/// The interface separates the administrative UI from SQLite and file-system details. Callers deal
/// only in validated backup file names and metadata, allowing the implementation to enforce path,
/// concurrency, and integrity rules in one infrastructure boundary.
/// </remarks>
public interface IDatabaseBackupService
{
    /// <summary>Returns all available backups in reverse chronological order.</summary>
    /// <param name="cancellationToken">A token used to cancel the operation before enumeration.</param>
    /// <returns>
    /// A read-only snapshot of backup metadata, or an empty list when the backup directory does
    /// not yet exist.
    /// </returns>
    Task<IReadOnlyList<DatabaseBackup>> GetBackupsAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates and validates a point-in-time copy of the active SQLite database.</summary>
    /// <param name="cancellationToken">A token used to cancel database and file operations.</param>
    /// <returns>Metadata for the newly created backup.</returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled.
    /// </exception>
    Task<DatabaseBackup> CreateBackupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the active database contents with a validated backup after first preserving the
    /// current database as a pre-restore snapshot.
    /// </summary>
    /// <param name="fileName">The leaf file name of a backup returned by <see cref="GetBackupsAsync"/>.</param>
    /// <param name="cancellationToken">A token used to cancel validation and database operations.</param>
    /// <returns>A task representing the restore operation.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="fileName"/> contains path segments or does not use the backup extension.
    /// </exception>
    /// <exception cref="FileNotFoundException">Thrown when the selected backup no longer exists.</exception>
    /// <exception cref="InvalidDataException">Thrown when the selected backup fails SQLite validation.</exception>
    Task RestoreAsync(string fileName, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implements consistent, validated backup and restore operations for the application's SQLite database.
/// </summary>
/// <remarks>
/// <para>
/// SQLite's native online backup API copies pages through database connections instead of copying
/// a potentially changing file. Every produced or consumed backup is checked with
/// <c>PRAGMA quick_check</c>, and failed backup attempts remove their partial destination file.
/// </para>
/// <para>
/// A process-local <see cref="SemaphoreSlim"/> serializes create and restore operations because
/// both workflows interact with the same active database and backup directory. This service is
/// registered as a singleton; the lock does not coordinate multiple application processes, so a
/// distributed deployment would require an external coordination mechanism.
/// </para>
/// </remarks>
public sealed class DatabaseBackupService : IDatabaseBackupService, IDisposable
{
    private const string BackupExtension = ".db";
    private readonly string connectionString;
    private readonly string backupDirectory;
    private readonly SemaphoreSlim operationLock = new(1, 1);

    /// <summary>
    /// Initializes a service using the active database connection and backup directory from
    /// application configuration.
    /// </summary>
    /// <param name="configuration">
    /// Configuration containing <c>ConnectionStrings:DefaultConnection</c> and optionally
    /// <c>DatabaseBackups:Directory</c>.
    /// </param>
    /// <param name="environment">
    /// The host environment whose content root resolves a relative backup directory.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when the default connection string is absent.</exception>
    public DatabaseBackupService(IConfiguration configuration, IWebHostEnvironment environment)
        : this(
            configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found."),
            ResolveBackupDirectory(configuration, environment))
    {
    }

    /// <summary>
    /// Initializes a service with explicit storage locations, primarily for isolated hosting and testing.
    /// </summary>
    /// <param name="connectionString">A SQLite connection string for the active application database.</param>
    /// <param name="backupDirectory">The directory in which backup files are stored.</param>
    public DatabaseBackupService(string connectionString, string backupDirectory)
    {
        this.connectionString = connectionString;
        this.backupDirectory = Path.GetFullPath(backupDirectory);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DatabaseBackup>> GetBackupsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!Directory.Exists(backupDirectory))
        {
            return Task.FromResult<IReadOnlyList<DatabaseBackup>>([]);
        }

        var backups = new DirectoryInfo(backupDirectory)
            .EnumerateFiles($"*{BackupExtension}", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.CreationTimeUtc)
            .Select(ToBackup)
            .ToList();

        return Task.FromResult<IReadOnlyList<DatabaseBackup>>(backups);
    }

    /// <inheritdoc />
    public async Task<DatabaseBackup> CreateBackupAsync(CancellationToken cancellationToken = default)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            return await CreateBackupCoreAsync("backup", cancellationToken);
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Validation occurs inside the operation lock to prevent a concurrent in-process operation
    /// from changing restore state. A pre-restore snapshot is mandatory and is validated through
    /// the same creation path as a normal backup before the destination database is overwritten.
    /// </remarks>
    public async Task RestoreAsync(string fileName, CancellationToken cancellationToken = default)
    {
        var sourcePath = GetExistingBackupPath(fileName);

        await operationLock.WaitAsync(cancellationToken);
        try
        {
            await ValidateDatabaseAsync(sourcePath, cancellationToken);
            await CreateBackupCoreAsync("pre-restore", cancellationToken);

            await using var source = new SqliteConnection(CreateConnectionString(sourcePath, readOnly: true));
            await using var destination = new SqliteConnection(connectionString);
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            operationLock.Release();
        }
    }

    /// <summary>Releases the synchronization primitive owned by this singleton service.</summary>
    public void Dispose() => operationLock.Dispose();

    /// <summary>
    /// Performs the shared backup algorithm while the caller holds <see cref="operationLock"/>.
    /// </summary>
    /// <param name="prefix">The file-name prefix identifying a manual or pre-restore backup.</param>
    /// <param name="cancellationToken">A token used to cancel SQLite operations.</param>
    /// <returns>Metadata for the validated backup file.</returns>
    /// <remarks>
    /// A timestamp makes files easy to inspect operationally, while a GUID prevents collisions
    /// between concurrent hosts and repeated operations within the same millisecond.
    /// </remarks>
    private async Task<DatabaseBackup> CreateBackupCoreAsync(string prefix, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(backupDirectory);
        var fileName = $"{prefix}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{Guid.NewGuid():N}{BackupExtension}";
        var destinationPath = Path.Combine(backupDirectory, fileName);

        try
        {
            await using var source = new SqliteConnection(connectionString);
            await using var destination = new SqliteConnection(CreateConnectionString(destinationPath));
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);
            source.BackupDatabase(destination);
            await ValidateDatabaseAsync(destinationPath, cancellationToken);
            return ToBackup(new FileInfo(destinationPath));
        }
        catch
        {
            File.Delete(destinationPath);
            throw;
        }
    }

    /// <summary>Runs SQLite's lightweight whole-database integrity check against a backup file.</summary>
    /// <param name="path">The full path to the database file to inspect in read-only mode.</param>
    /// <param name="cancellationToken">A token used to cancel the database operation.</param>
    /// <exception cref="InvalidDataException">Thrown when SQLite reports any integrity problem.</exception>
    private static async Task ValidateDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(CreateConnectionString(path, readOnly: true));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (!string.Equals(result?.ToString(), "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The selected backup failed SQLite integrity validation.");
        }
    }

    /// <summary>
    /// Resolves a client-supplied backup name only after constraining it to a leaf <c>.db</c> file
    /// in the configured backup directory.
    /// </summary>
    /// <param name="fileName">The untrusted leaf file name supplied by the administrative UI.</param>
    /// <returns>The full path of an existing backup.</returns>
    /// <remarks>The leaf-name comparison prevents directory traversal and arbitrary file restores.</remarks>
    private string GetExistingBackupPath(string fileName)
    {
        if (!string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
            || !fileName.EndsWith(BackupExtension, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Invalid backup file name.", nameof(fileName));
        }

        var path = Path.Combine(backupDirectory, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The selected backup no longer exists.", fileName);
        }

        return path;
    }

    /// <summary>Builds a non-pooled SQLite connection string for a database file.</summary>
    /// <remarks>
    /// Disabling pooling prevents backup and restore operations from retaining file handles or
    /// reusing connections whose view of the replaced database may be stale.
    /// </remarks>
    private static string CreateConnectionString(string dataSource, bool readOnly = false) => new SqliteConnectionStringBuilder
    {
        DataSource = dataSource,
        Mode = readOnly ? SqliteOpenMode.ReadOnly : SqliteOpenMode.ReadWriteCreate,
        Pooling = false
    }.ToString();

    /// <summary>Resolves the configured backup directory relative to the application content root.</summary>
    private static string ResolveBackupDirectory(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredDirectory = configuration["DatabaseBackups:Directory"] ?? "App_Data/Backups";
        return Path.IsPathRooted(configuredDirectory)
            ? configuredDirectory
            : Path.Combine(environment.ContentRootPath, configuredDirectory);
    }

            /// <summary>Projects file-system metadata into the service's transport-neutral backup model.</summary>
    private static DatabaseBackup ToBackup(FileInfo file) => new(
        file.Name,
        file.CreationTimeUtc,
        file.Length,
        file.Name.StartsWith("pre-restore-", StringComparison.Ordinal));
}