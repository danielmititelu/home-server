namespace Vaultling.Models;

using System.Globalization;

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

            // Create calendar-like table
            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            var firstDay = new DateTime(month.Year, month.Month, 1);
            var totalDays = month.DayWorkoutCounts.Count;

            sections.Add($"**Total Days: {totalDays}**");
            sections.Add("");

            // Create weekly rows
            sections.Add("| Mon | Tue | Wed | Thu | Fri | Sat | Sun |");
            sections.Add("|-----|-----|-----|-----|-----|-----|-----|");

            var currentWeek = new List<string>();
            var startDayOfWeek = (int)firstDay.DayOfWeek;
            // Adjust for Monday start (Monday = 0)
            startDayOfWeek = startDayOfWeek == 0 ? 6 : startDayOfWeek - 1;

            // Add empty cells before the first day
            for (int i = 0; i < startDayOfWeek; i++)
            {
                currentWeek.Add("     ");
            }

            // Add all days of the month
            for (int day = 1; day <= daysInMonth; day++)
            {
                var count = month.DayWorkoutCounts.GetValueOrDefault(day, 0);
                var cell = count > 0 ? $"✅ {day:00}" : $"⬜ {day:00}";
                currentWeek.Add(cell);

                // If we've filled a week (7 days), add the row
                if (currentWeek.Count == 7)
                {
                    sections.Add($"| {string.Join(" | ", currentWeek)} |");
                    currentWeek.Clear();
                }
            }

            // Add the last partial week if needed
            if (currentWeek.Count > 0)
            {
                while (currentWeek.Count < 7)
                {
                    currentWeek.Add("     ");
                }
                sections.Add($"| {string.Join(" | ", currentWeek)} |");
            }

            sections.Add("");
        }

        return sections;
    }
}
