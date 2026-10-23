using BoxingCoach.Web.Services;
using Microsoft.Data.Sqlite;

namespace BoxingCoach.Web.Tests;

[TestClass]
public sealed class DatabaseBackupServiceTests
{
    private string testDirectory = null!;
    private string databasePath = null!;
    private string backupDirectory = null!;
    private DatabaseBackupService service = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        testDirectory = Path.Combine(Path.GetTempPath(), $"boxing-coach-tests-{Guid.NewGuid():N}");
        databasePath = Path.Combine(testDirectory, "site.db");
        backupDirectory = Path.Combine(testDirectory, "backups");
        Directory.CreateDirectory(testDirectory);

        var connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath, Pooling = false }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE Settings (Value TEXT NOT NULL); INSERT INTO Settings VALUES ('original');";
        await command.ExecuteNonQueryAsync();

        service = new DatabaseBackupService(connectionString, backupDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        service.Dispose();
        SqliteConnection.ClearAllPools();
        Directory.Delete(testDirectory, recursive: true);
    }

    [TestMethod]
    public async Task CreateBackupAsync_CreatesValidListedSnapshot()
    {
        var created = await service.CreateBackupAsync();

        var backups = await service.GetBackupsAsync();

        Assert.HasCount(1, backups);
        Assert.AreEqual(created.FileName, backups[0].FileName);
        Assert.IsGreaterThan(0, created.SizeBytes);
        Assert.IsTrue(File.Exists(Path.Combine(backupDirectory, created.FileName)));
    }

    [TestMethod]
    public async Task RestoreAsync_RestoresDataAndCreatesSafetySnapshot()
    {
        var backup = await service.CreateBackupAsync();
        await SetDatabaseValueAsync("changed");

        await service.RestoreAsync(backup.FileName);

        Assert.AreEqual("original", await GetDatabaseValueAsync());
        var backups = await service.GetBackupsAsync();
        Assert.HasCount(2, backups);
        Assert.IsTrue(backups.Any(item => item.IsPreRestoreSnapshot));
    }

    [TestMethod]
    public async Task RestoreAsync_CorruptBackup_DoesNotReplaceDatabase()
    {
        Directory.CreateDirectory(backupDirectory);
        await File.WriteAllTextAsync(Path.Combine(backupDirectory, "corrupt.db"), "not a sqlite database");

        await Assert.ThrowsAsync<SqliteException>(() => service.RestoreAsync("corrupt.db"));

        Assert.AreEqual("original", await GetDatabaseValueAsync());
        Assert.IsFalse((await service.GetBackupsAsync()).Any(item => item.IsPreRestoreSnapshot));
    }

    [TestMethod]
    public async Task GetBackupsAsync_WhenDirectoryMissing_ReturnsEmptyList()
    {
        var missingService = new DatabaseBackupService("Data Source=:memory:", Path.Combine(testDirectory, "does-not-exist"));

        var backups = await missingService.GetBackupsAsync();

        Assert.IsEmpty(backups);
    }

    [TestMethod]
    public async Task GetBackupsAsync_IgnoresNonDatabaseFilesAndSortsNewestFirst()
    {
        Directory.CreateDirectory(backupDirectory);
        var oldest = Path.Combine(backupDirectory, "older.db");
        var newest = Path.Combine(backupDirectory, "newer.db");
        var note = Path.Combine(backupDirectory, "notes.txt");

        var olderTimestamp = DateTime.UtcNow.AddDays(-2);
        var newerTimestamp = DateTime.UtcNow.AddDays(-1);

        await File.WriteAllTextAsync(oldest, "older");
        await File.WriteAllTextAsync(newest, "newer");
        await File.WriteAllTextAsync(note, "ignore me");
        File.SetCreationTimeUtc(oldest, olderTimestamp);
        File.SetCreationTimeUtc(newest, newerTimestamp);

        var backups = await service.GetBackupsAsync();

        Assert.AreEqual("newer.db", backups[0].FileName);
        Assert.AreEqual("older.db", backups[1].FileName);
        Assert.IsFalse(backups.Any(item => item.FileName == "notes.txt"));
    }

    [TestMethod]
    public async Task RestoreAsync_UnsupportedFileExtension_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.RestoreAsync("backup.txt"));
        Assert.AreEqual("original", await GetDatabaseValueAsync());
    }

    [TestMethod]
    public async Task CreateBackupAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(() => service.CreateBackupAsync(cancellationSource.Token));
    }

    [TestMethod]
    public async Task RestoreAsync_PathTraversal_IsRejected()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => service.RestoreAsync("../site.db"));
        Assert.AreEqual("original", await GetDatabaseValueAsync());
    }

    [TestMethod]
    public async Task CreateBackupAsync_ConcurrentRequests_CreateDistinctSnapshots()
    {
        await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => service.CreateBackupAsync()));

        Assert.HasCount(5, await service.GetBackupsAsync());
    }

    private async Task SetDatabaseValueAsync(string value)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE Settings SET Value = $value;";
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }

    private async Task<string?> GetDatabaseValueAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings;";
        return (string?)await command.ExecuteScalarAsync();
    }
}