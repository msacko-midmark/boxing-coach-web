using System.ComponentModel.DataAnnotations;

namespace BoxingCoach.Web.Data;

public class WorkoutPlan
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(40)]
    public string Focus { get; set; } = string.Empty;

    [MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [Range(10, 180)]
    public int DurationMinutes { get; set; }

    [Range(1, 5)]
    public int Difficulty { get; set; }

    public ICollection<WorkoutExercise> Exercises { get; set; } = [];
}

public class WorkoutExercise
{
    public int Id { get; set; }

    public int WorkoutPlanId { get; set; }

    public WorkoutPlan WorkoutPlan { get; set; } = null!;

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(180)]
    public string CoachingCue { get; set; } = string.Empty;

    [Range(1, 20)]
    public int Rounds { get; set; }

    [Range(15, 600)]
    public int SecondsPerRound { get; set; }

    public int Sequence { get; set; }
}