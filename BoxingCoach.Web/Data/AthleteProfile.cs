using System.ComponentModel.DataAnnotations;

namespace BoxingCoach.Web.Data;

/// <summary>
/// Captures the stable training preferences used to personalize an athlete's coaching experience.
/// </summary>
/// <remarks>
/// This entity is the personalization input for the recommendation layer rather than a record of
/// an individual workout. <see cref="ApplicationDbContext"/> enforces one profile per Identity
/// user, while validation attributes provide shared constraints for UI validation and persistence.
/// </remarks>
public class AthleteProfile
{
    /// <summary>
    /// Gets or sets the database-generated primary key.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the ASP.NET Core Identity user identifier that owns this profile.
    /// </summary>
    /// <remarks>The database model applies a unique index to enforce one profile per user.</remarks>
    [Required]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the athlete's self-assessed boxing experience on a scale from 1 to 5.
    /// </summary>
    /// <remarks>This value is compared with <see cref="WorkoutPlan.Difficulty"/> during ranking.</remarks>
    [Range(1, 5)]
    public int ExperienceLevel { get; set; } = 1;

    /// <summary>
    /// Gets or sets the desired number of training days per week, from 1 through 7.
    /// </summary>
    [Range(1, 7)]
    public int TrainingDaysPerWeek { get; set; } = 3;

    /// <summary>
    /// Gets or sets the preferred workout duration in minutes.
    /// </summary>
    /// <remarks>This value is compared with <see cref="WorkoutPlan.DurationMinutes"/> during ranking.</remarks>
    [Range(10, 180)]
    public int PreferredMinutes { get; set; } = 45;

    /// <summary>
    /// Gets or sets the training focus the athlete most wants to improve, such as Technique,
    /// Conditioning, Defense, Power, or Recovery.
    /// </summary>
    /// <remarks>
    /// The recommendation policy compares this value with <see cref="WorkoutPlan.Focus"/> using
    /// an ordinal, case-insensitive match.
    /// </remarks>
    [MaxLength(40)]
    public string PrimaryGoal { get; set; } = "Technique";
}