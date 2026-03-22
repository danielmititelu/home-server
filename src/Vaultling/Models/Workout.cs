namespace Vaultling.Models;

public record MonthlyWorkoutSummary(
    int Month,
    int Year,
    Dictionary<int, int> DayWorkoutCounts
);

public record WorkoutLog(string Month, string Day, string Type, string Reps);
