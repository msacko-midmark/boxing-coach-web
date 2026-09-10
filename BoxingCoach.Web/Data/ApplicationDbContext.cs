using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BoxingCoach.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AthleteProfile> AthleteProfiles => Set<AthleteProfile>();

    public DbSet<WorkoutPlan> WorkoutPlans => Set<WorkoutPlan>();

    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();

    public DbSet<WorkoutSession> WorkoutSessions => Set<WorkoutSession>();

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
