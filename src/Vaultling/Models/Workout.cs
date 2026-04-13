namespace Vaultling.Models;

public record MonthlyWorkoutSummary(
    int Month,
    int Year,
    Dictionary<int, int> DayWorkoutCounts
);

public record WorkoutLog(int Month, int Day, string Type, string Reps);
