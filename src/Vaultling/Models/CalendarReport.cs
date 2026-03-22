namespace Vaultling.Models;

using System.Globalization;

public record MonthlyCalendarSummary(
    int Month,
    int Year,
    List<CalendarOccurrence> Events
);

public record CalendarReport(List<MonthlyCalendarSummary> Months)
{
    public IEnumerable<string> ToMarkdownLines()
    {
        var sections = new List<string>();
        var today = DateTime.Today;

        foreach (var month in Months)
        {
            var monthName = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month.Month);
            var isCurrentMonth = month.Year == today.Year && month.Month == today.Month;
            sections.Add($"## {month.Month:00} - {monthName}{(isCurrentMonth ? " ⭐" : "")}");
            sections.Add("");

            var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
            var firstDay = new DateTime(month.Year, month.Month, 1);
            var eventDays = month.Events.Select(e => e.Date.Day).ToHashSet();

            sections.Add("| Mon | Tue | Wed | Thu | Fri | Sat | Sun |");
            sections.Add("|-----|-----|-----|-----|-----|-----|-----|");

            var currentWeek = new List<string>();
            var startDayOfWeek = (int)firstDay.DayOfWeek;
            startDayOfWeek = startDayOfWeek == 0 ? 6 : startDayOfWeek - 1;

            for (int i = 0; i < startDayOfWeek; i++)
                currentWeek.Add("     ");

            for (int day = 1; day <= daysInMonth; day++)
            {
                var isToday = isCurrentMonth && day == today.Day;
                var cell = isToday
                    ? $"🔵 {day:00}"
                    : eventDays.Contains(day) ? $"✅ {day:00}" : $"⬜ {day:00}";
                currentWeek.Add(cell);

                if (currentWeek.Count == 7)
                {
                    sections.Add($"| {string.Join(" | ", currentWeek)} |");
                    currentWeek.Clear();
                }
            }

            if (currentWeek.Count > 0)
            {
                while (currentWeek.Count < 7)
                    currentWeek.Add("     ");
                sections.Add($"| {string.Join(" | ", currentWeek)} |");
            }

            if (month.Events.Count > 0)
            {
                sections.Add("");
                foreach (var evt in month.Events.OrderBy(e => e.Date))
                {
                    var timeStr = evt.Date.TimeOfDay == TimeSpan.Zero
                        ? ""
                        : $" at {evt.Date:HH:mm}";
                    sections.Add($"- {evt.Date.Day:00}{timeStr}: {evt.Note}");
                }
            }

            sections.Add("");
        }

        return sections;
    }
}
