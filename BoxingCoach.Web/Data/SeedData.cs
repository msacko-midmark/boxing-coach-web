using Microsoft.EntityFrameworkCore;

namespace BoxingCoach.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await dbContext.Database.MigrateAsync();

        if (await dbContext.WorkoutPlans.AnyAsync())
        {
            return;
        }

        dbContext.WorkoutPlans.AddRange(
            CreatePlan("Sharp Fundamentals", "Technique", "Build clean mechanics, balance, and a reliable one-two.", 35, 1,
                ("Footwork lines", "Stay over your hips and finish every step in stance.", 3, 120),
                ("Jab-only shadowboxing", "Snap the hand home and keep the rear hand glued to your cheek.", 3, 180),
                ("One-two on the bag", "Turn the rear hip through without reaching.", 4, 180)),
            CreatePlan("Engine Room", "Conditioning", "Fight-paced intervals that train repeatable output under fatigue.", 45, 3,
                ("Jump rope", "Relax the shoulders and breathe through the rhythm.", 3, 180),
                ("Heavy bag power intervals", "Work for 20 seconds, move for 20 seconds, then reset.", 6, 180),
                ("Punch-out finisher", "Straight punches, compact form, steady feet.", 4, 30)),
            CreatePlan("Counterpunch Lab", "Defense", "Read, defend, and answer without surrendering position.", 40, 3,
                ("Slip-line movement", "Move the head just outside the punch, not outside your stance.", 3, 180),
                ("Catch-and-shoot drill", "Catch the jab and return yours before the hand gets home.", 4, 180),
                ("Roll and counter", "Bend at the knees, keep your eyes level, and exit on an angle.", 3, 180)),
            CreatePlan("Heavy Hands", "Power", "Develop force from the floor while preserving shape and recovery.", 50, 4,
                ("Medicine ball rotational throw", "Drive from the floor and finish through the target.", 4, 45),
                ("Heavy bag power combinations", "Every hard shot starts balanced and ends guarded.", 6, 180),
                ("Body-shot rounds", "Change levels with the legs and bring the hand back high.", 3, 180)),
            CreatePlan("Recovery Rounds", "Recovery", "Low-intensity movement for skill retention and active recovery.", 25, 1,
                ("Easy rope", "Keep the pace conversational.", 3, 120),
                ("Flow shadowboxing", "No power. Stay loose and move after every combination.", 4, 120),
                ("Mobility reset", "Open the hips, thoracic spine, and shoulders without forcing range.", 2, 180)));

        await dbContext.SaveChangesAsync();
    }

    private static WorkoutPlan CreatePlan(
        string name,
        string focus,
        string description,
        int duration,
        int difficulty,
        params (string Name, string Cue, int Rounds, int Seconds)[] exercises)
    {
        return new WorkoutPlan
        {
            Name = name,
            Focus = focus,
            Description = description,
            DurationMinutes = duration,
            Difficulty = difficulty,
            Exercises = exercises.Select((exercise, index) => new WorkoutExercise
            {
                Name = exercise.Name,
                CoachingCue = exercise.Cue,
                Rounds = exercise.Rounds,
                SecondsPerRound = exercise.Seconds,
                Sequence = index + 1
            }).ToList()
        };
    }
}