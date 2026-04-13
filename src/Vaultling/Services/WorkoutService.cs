namespace Vaultling.Services;

using System.Globalization;
using Vaultling.Utils;

public class WorkoutService(WorkoutRepository workoutRepository, TimeProvider timeProvider)
{
    public void ProduceWorkoutReport()
    {
        var logs = workoutRepository.ReadWorkoutLogs();
        var currentYear = timeProvider.GetLocalNow().Year;

        // Group workouts by month and day, counting occurrences
        var months = logs
            .GroupBy(l => new { l.Month, Year = currentYear })
            .OrderBy(g => g.Key.Month)
            .Select(monthGroup =>
            {
                var dayWorkoutCounts = monthGroup
                    .GroupBy(l => l.Day)
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

        workoutRepository.WriteWorkoutReport(GenerateMarkdownWorkoutReport(months));
    }

    private static string GenerateMarkdownWorkoutReport(List<MonthlyWorkoutSummary> months)
    {
        var sections = new List<string>();

        foreach (var month in months)
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Month);
            var totalDays = month.DayWorkoutCounts.Count;
            var calendarGrid = Utils.BuildMonthlyCalendar(
                month.Year,
                month.Month,
                day =>
                {
                    var count = month.DayWorkoutCounts.GetValueOrDefault(day, 0);
                    return count > 0 ? $"✅ {day:00}" : $"⬜ {day:00}";
                });

            var monthSection = $"""
                ## {month.Month:00} - {monthName}

                **Total Days: {totalDays}**

                {calendarGrid}

                """;

            sections.Add(monthSection);
        }

        return string.Join("", sections);
    }
}
