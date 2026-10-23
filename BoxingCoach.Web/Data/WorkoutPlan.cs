using System.ComponentModel.DataAnnotations;

namespace BoxingCoach.Web.Data;

/// <summary>
/// Defines a reusable, coach-authored workout template that can be recommended to many athletes.
/// </summary>
/// <remarks>
/// A plan describes intended training, while <see cref="WorkoutSession"/> records completed
/// training. Keeping those concepts separate allows the catalog to be reused without duplicating
/// exercise definitions for every athlete and preserves history through a stable plan reference.
/// </remarks>
public class WorkoutPlan
{
    /// <summary>Gets or sets the database-generated primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the human-readable catalog name displayed to athletes.</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the principal coaching objective used as the strongest recommendation signal.
    /// </summary>
    [Required, MaxLength(40)]
    public string Focus { get; set; } = string.Empty;

    /// <summary>Gets or sets a concise explanation of the plan's training intent.</summary>
    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the expected total workout duration in minutes.</summary>
    [Range(10, 180)]
    public int DurationMinutes { get; set; }

    /// <summary>Gets or sets the intended athlete experience level on a scale from 1 to 5.</summary>
    [Range(1, 5)]
    public int Difficulty { get; set; }

    /// <summary>
    /// Gets or sets the ordered exercise definitions that compose this plan.
    /// </summary>
    /// <remarks>
    /// EF Core treats these records as dependents: deleting a plan cascades to its exercises.
    /// Consumers should order the collection by <see cref="WorkoutExercise.Sequence"/> when
    /// presenting or executing the workout.
    /// </remarks>
    public ICollection<WorkoutExercise> Exercises { get; set; } = [];
}

/// <summary>
/// Defines one ordered drill, interval, or movement within a <see cref="WorkoutPlan"/>.
/// </summary>
/// <remarks>
/// Exercises are catalog definitions rather than completion records. Their lifecycle is owned by
/// the parent plan, and the combination of rounds and seconds per round describes prescribed work.
/// </remarks>
public class WorkoutExercise
{
    /// <summary>Gets or sets the database-generated primary key.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the foreign key of the plan that owns this exercise.</summary>
    public int WorkoutPlanId { get; set; }

    /// <summary>Gets or sets the plan navigation associated with <see cref="WorkoutPlanId"/>.</summary>
    public WorkoutPlan WorkoutPlan { get; set; } = null!;

    /// <summary>Gets or sets the drill name displayed in the workout sequence.</summary>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the short technical instruction that explains how to perform the drill well.
    /// </summary>
    [MaxLength(180)]
    public string CoachingCue { get; set; } = string.Empty;

    /// <summary>Gets or sets the prescribed number of work rounds, from 1 through 20.</summary>
    [Range(1, 20)]
    public int Rounds { get; set; }

    /// <summary>Gets or sets the prescribed duration of each round in seconds.</summary>
    [Range(15, 600)]
    public int SecondsPerRound { get; set; }

    /// <summary>
    /// Gets or sets the one-based display and execution position of the exercise within its plan.
    /// </summary>
    public int Sequence { get; set; }
}