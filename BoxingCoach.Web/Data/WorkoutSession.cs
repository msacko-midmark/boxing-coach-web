using System.ComponentModel.DataAnnotations;

namespace BoxingCoach.Web.Data;

/// <summary>
/// Records an athlete's completion of a workout plan and their subjective training feedback.
/// </summary>
/// <remarks>
/// This entity forms the workout-history boundary. It references a reusable
/// <see cref="WorkoutPlan"/> but stores completion-specific facts such as actual duration, effort,
/// rating, and notes. The database restricts deletion of referenced plans to preserve the meaning
/// of historical sessions.
/// </remarks>
public class WorkoutSession
{
    /// <summary>Gets or sets the database-generated primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the Identity user identifier of the athlete who completed the workout.</summary>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>Gets or sets the foreign key of the workout template that was completed.</summary>
    public int WorkoutPlanId { get; set; }

    /// <summary>Gets or sets the workout-plan navigation associated with <see cref="WorkoutPlanId"/>.</summary>
    public WorkoutPlan WorkoutPlan { get; set; } = null!;

    /// <summary>
    /// Gets or sets the UTC timestamp at which the workout was completed.
    /// </summary>
    /// <remarks>UTC storage keeps history unambiguous across server and athlete time zones.</remarks>
    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the athlete's actual workout duration in minutes.</summary>
    [Range(1, 300)]
    public int DurationMinutes { get; set; }

    /// <summary>Gets or sets perceived exertion on a scale from 1 to 10.</summary>
    [Range(1, 10)]
    public int Effort { get; set; } = 5;

    /// <summary>Gets or sets the athlete's workout-quality rating on a scale from 1 to 5.</summary>
    [Range(1, 5)]
    public int Rating { get; set; } = 3;

    /// <summary>Gets or sets optional free-form reflections captured after training.</summary>
    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;
}