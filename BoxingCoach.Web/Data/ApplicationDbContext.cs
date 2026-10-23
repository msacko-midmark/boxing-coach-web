using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoxingCoach.Web.Data;

/// <summary>
/// Represents the application's unit-of-work boundary for identity, athlete, workout-plan, and
/// workout-history data.
/// </summary>
/// <param name="options">
/// The provider and connection options assembled by the application composition root.
/// </param>
/// <remarks>
/// <para>
/// The context inherits from <see cref="IdentityDbContext{TUser}"/> so ASP.NET Core Identity and
/// boxing-specific entities share one SQLite database, one migration history, and one transaction
/// boundary. Identity tables remain managed by the base context; this type adds only the
/// application-specific sets and relationships.
/// </para>
/// <para>
/// Interactive components and application services should obtain contexts from
/// <see cref="IDbContextFactory{TContext}"/> and keep each instance operation-scoped. An EF Core
/// context is mutable and not thread-safe, so it must not be retained for the lifetime of a
/// Blazor circuit.
/// </para>
/// </remarks>
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    /// <summary>
    /// Gets the one-per-user training preferences used to personalize plan recommendations.
    /// </summary>
    public DbSet<AthleteProfile> AthleteProfiles => Set<AthleteProfile>();

    /// <summary>
    /// Gets the reusable workout templates available in the coaching catalog.
    /// </summary>
    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();

    /// <summary>
    /// Gets the ordered exercise definitions owned by workout plans.
    /// </summary>
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();

    /// <summary>
    /// Gets the immutable-in-intent records of workouts completed by athletes.
    /// </summary>
    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();

    /// <summary>
    /// Configures relational constraints that cannot be expressed completely through data
    /// annotations on the domain entities.
    /// </summary>
    /// <param name="builder">The model builder for the current context model.</param>
    /// <remarks>
    /// Profile ownership is enforced with a unique user identifier. Exercises are compositionally
    /// owned by a plan and cascade on deletion, while historical sessions restrict plan deletion
    /// so a completed workout can never silently lose the template that gives it meaning.
    /// </remarks>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AthleteProfile>()
            .HasIndex(profile => profile.UserId)
            .IsUnique();

        builder.Entity<WorkoutPlan>()
            .HasMany(plan => plan.Exercises)
            .WithOne(exercise => exercise.WorkoutPlan)
            .HasForeignKey(exercise => exercise.WorkoutPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<WorkoutSession>()
            .HasOne(session => session.WorkoutPlan)
            .WithMany()
            .HasForeignKey(session => session.WorkoutPlanId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
