namespace Vaultling.Models;

using System.Globalization;
using Vaultling.Utils;

public record MonthlyWorkoutSummary(
    int Month,
    int Year,
    Dictionary<int, int> DayWorkoutCounts
);

public record WorkoutReport(List<MonthlyWorkoutSummary> Months)
{
    public IEnumerable<string> ToMarkdownLines()
    {
        var sections = new List<string> {};

        foreach (var month in Months)
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Month);
            sections.Add($"## {month.Month:00} - {monthName}");
            sections.Add("");

            var totalDays = month.DayWorkoutCounts.Count;

            sections.Add($"**Total Days: {totalDays}**");
            sections.Add("");

            MarkdownCalendarRenderer.AppendCalendarGrid(
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
