using BoxingCoach.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BoxingCoach.Web.Services;

public class CoachRecommendationService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
{
    public async Task<IReadOnlyList<WorkoutPlan>> GetRecommendationsAsync(
        AthleteProfile profile,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var plans = await dbContext.WorkoutPlans
            .Include(plan => plan.Exercises)
            .ToListAsync(cancellationToken);

        return plans
            .OrderBy(plan => plan.Focus.Equals(profile.PrimaryGoal, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(plan => Math.Abs(plan.Difficulty - profile.ExperienceLevel))
            .ThenBy(plan => Math.Abs(plan.DurationMinutes - profile.PreferredMinutes))
            .Take(3)
            .ToList();
    }
}