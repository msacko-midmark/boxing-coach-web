using BoxingCoach.Web.Data;
using BoxingCoach.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BoxingCoach.Web.Tests;

[TestClass]
public class CoachRecommendationServiceTests
{
    [TestMethod]
    public async Task GetRecommendationsAsync_MatchingGoal_PrioritizesMatchingFocus()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var dbContext = new ApplicationDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
            dbContext.WorkoutPlans.AddRange(
                new WorkoutPlan { Name = "Easy technique", Focus = "Technique", Difficulty = 1, DurationMinutes = 30 },
                new WorkoutPlan { Name = "Power match", Focus = "Power", Difficulty = 4, DurationMinutes = 45 });
            await dbContext.SaveChangesAsync();
        }

        var service = new CoachRecommendationService(new TestDbContextFactory(options));
        var profile = new AthleteProfile { PrimaryGoal = "Power", ExperienceLevel = 1, PreferredMinutes = 30 };

        var recommendations = await service.GetRecommendationsAsync(profile);

        Assert.AreEqual("Power match", recommendations[0].Name);
    }

    [TestMethod]
    public async Task GetRecommendationsAsync_MoreThanThreePlans_ReturnsThree()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var dbContext = new ApplicationDbContext(options))
        {
            await dbContext.Database.EnsureCreatedAsync();
            for (var index = 1; index <= 4; index++)
            {
                dbContext.WorkoutPlans.Add(new WorkoutPlan
                {
                    Name = $"Plan {index}",
                    Focus = "Technique",
                    Difficulty = index,
                    DurationMinutes = 30
                });
            }
            await dbContext.SaveChangesAsync();
        }

        var service = new CoachRecommendationService(new TestDbContextFactory(options));

        var recommendations = await service.GetRecommendationsAsync(new AthleteProfile());

        Assert.HasCount(3, recommendations);
    }

    private sealed class TestDbContextFactory(DbContextOptions<ApplicationDbContext> options)
        : IDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext() => new(options);
    }
}