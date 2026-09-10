using System.ComponentModel.DataAnnotations;

namespace BoxingCoach.Web.Data;

public class WorkoutSession
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public int WorkoutPlanId { get; set; }

    public WorkoutPlan WorkoutPlan { get; set; } = null!;

    public DateTime CompletedAtUtc { get; set; } = DateTime.UtcNow;

    [Range(1, 300)]
    public int DurationMinutes { get; set; }

    [Range(1, 10)]
    public int Effort { get; set; } = 5;

    [Range(1, 5)]
    public int Rating { get; set; } = 3;

    [MaxLength(500)]
    public string Notes { get; set; } = string.Empty;
}