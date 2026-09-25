using BoxingCoach.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace BoxingCoach.Web.Services;

/// <summary>
/// Selects workout plans that best fit an athlete's current training profile.
/// </summary>
/// <remarks>
/// <para>
/// This type is an application service: it coordinates a read from the persistence layer and
/// applies recommendation policy, but it does not own HTTP, UI, or entity persistence concerns.
/// Razor components can therefore request recommendations without knowing how plans are loaded
/// or how candidates are ranked.
/// </para>
/// <para>
/// A new <see cref="ApplicationDbContext"/> is created for each operation through
/// <see cref="IDbContextFactory{TContext}"/>. This matches the Blazor Server execution model,
/// where a scoped service can outlive a single database operation and a shared context would be
/// vulnerable to concurrent use.
/// </para>
/// </remarks>
public class CoachRecommendationService(IDbContextFactory<ApplicationDbContext> dbContextFactory)
{
    /// <summary>
    /// Returns the three workout plans whose focus, difficulty, and duration most closely match
    /// the supplied athlete profile.
    /// </summary>
    /// <param name="profile">
    /// The athlete preferences used to rank plans. The profile is treated as input only and is
    /// not attached to the database context or modified by this operation.
    /// </param>
    /// <param name="cancellationToken">
    /// A token that cancels database creation and plan loading. Ranking is performed in memory
    /// after the query has completed.
    /// </param>
    /// <returns>
    /// A read-only list containing at most three plans, ordered first by an exact focus match,
    /// then by the smallest difficulty difference, and finally by the smallest duration
    /// difference. Each returned plan includes its exercises.
    /// </returns>
    /// <remarks>
    /// The deliberately small plan catalog is loaded before ranking because the policy uses
    /// case-insensitive string comparison and absolute-distance calculations. If the catalog
    /// becomes large, this boundary is the appropriate place to move ranking into a query or a
    /// dedicated recommendation engine without changing callers.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when <paramref name="cancellationToken"/> is canceled before the database work
    /// completes.
    /// </exception>
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