using System.ComponentModel.DataAnnotations;

namespace BoxingCoach.Web.Data;

public class AthleteProfile
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Range(1, 5)]
    public int ExperienceLevel { get; set; } = 1;

    [Range(1, 7)]
    public int TrainingDaysPerWeek { get; set; } = 3;

    [Range(10, 180)]
    public int PreferredMinutes { get; set; } = 45;

    [MaxLength(40)]
    public string PrimaryGoal { get; set; } = "Technique";
}