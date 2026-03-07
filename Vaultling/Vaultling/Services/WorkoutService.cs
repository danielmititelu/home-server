namespace Vaultling.Services;

public class WorkoutService(WorkoutRepository workoutRepository, TimeProvider timeProvider)
{
    public void ProduceWorkoutReport()
    {
        var logs = workoutRepository.ReadWorkoutLogs();
        var currentYear = timeProvider.GetLocalNow().Year;

        // Group workouts by month and day, counting occurrences
        var months = logs
            .GroupBy(l => new { Month = int.Parse(l.Month), Year = currentYear })
            .OrderBy(g => g.Key.Month)
            .Select(monthGroup =>
            {
                var dayWorkoutCounts = monthGroup
                    .GroupBy(l => int.Parse(l.Day))
                    .ToDictionary(
                        dayGroup => dayGroup.Key,
                        dayGroup => dayGroup.Count()
                    );

                return new MonthlyWorkoutSummary(
                    Month: monthGroup.Key.Month,
                    Year: monthGroup.Key.Year,
                    DayWorkoutCounts: dayWorkoutCounts
                );
            })
            .ToList();

        var report = new WorkoutReport(months);
        workoutRepository.WriteWorkoutReport(report);
    }
}
