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
            .Where(l =>
            {
                if (int.TryParse(l.Month, out _) && int.TryParse(l.Day, out _)) return true;
                Console.Error.WriteLine($"[WorkoutService] Skipping malformed log row: Month='{l.Month}' Day='{l.Day}'");
                return false;
            })
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

        workoutRepository.WriteWorkoutReport(GenerateMarkdownWorkoutReport(months));
    }

    private static List<string> GenerateMarkdownWorkoutReport(List<MonthlyWorkoutSummary> months)
    {
        var sections = new List<string>();

        foreach (var month in months)
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Month);
            sections.Add($"## {month.Month:00} - {monthName}");
            sections.Add("");

            var totalDays = month.DayWorkoutCounts.Count;
            sections.Add($"**Total Days: {totalDays}**");
            sections.Add("");

            Utils.AppendCalendarGrid(
                sections,
                month.Year,
                month.Month,
                day =>
                {
                    var count = month.DayWorkoutCounts.GetValueOrDefault(day, 0);
                    return count > 0 ? $"✅ {day:00}" : $"⬜ {day:00}";
                });

            sections.Add("");
        }

        return sections;
    }
}
