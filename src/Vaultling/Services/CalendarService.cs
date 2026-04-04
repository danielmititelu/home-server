namespace Vaultling.Services;
using Vaultling.Utils;

public class CalendarService(CalendarRepository calendarRepository, TimeProvider timeProvider)
{
    public void ProduceCalendarReport()
    {
        var now = timeProvider.GetLocalNow();
        var currentYear = now.Year;

        foreach (var year in Enumerable.Range(currentYear, 3))
        {
            var occurrences = calendarRepository.ReadCalendarOccurrences(year).ToList();

            var eventsByMonth = occurrences
                .GroupBy(o => o.Date.Month)
                .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Date).ToList());

            var months = Enumerable.Range(1, 12)
                .Select(m => new MonthlyCalendarSummary(
                    Month: m,
                    Year: year,
                    Events: eventsByMonth.GetValueOrDefault(m, [])
                ))
                .ToList();

            if (year == currentYear)
            {
                var existingSections = SplitReportIntoMonthSections(
                    calendarRepository.ReadCalendarReportLines(year));

                var frozenEventLines = existingSections.TryGetValue(now.Month, out var curSection)
                    ? ExtractPastEventLines(curSection, now.Day - 1)
                    : [];

                var futureMonths = months.Where(m => m.Month >= now.Month).ToList();
                var generatedLines = GenerateMarkdownForCalendarReport(
                    new CalendarReport(futureMonths),
                    frozenBeforeDay: now.Day,
                    frozenMonth: now.Month,
                    frozenEventLines: frozenEventLines);

                var pastLines = new List<string>();
                for (var m = 1; m < now.Month; m++)
                {
                    if (existingSections.TryGetValue(m, out var sectionLines))
                        pastLines.AddRange(sectionLines);
                }

                calendarRepository.WriteCalendarReport(year, [.. pastLines, .. generatedLines]);
            }
            else
            {
                var report = new CalendarReport(months);
                calendarRepository.WriteCalendarReport(year, GenerateMarkdownForCalendarReport(report));
            }
        }
    }

    private static List<string> GenerateMarkdownForCalendarReport(
        CalendarReport report,
        int frozenBeforeDay = 1,
        int frozenMonth = 0,
        List<string>? frozenEventLines = null)
    {
        var sections = new List<string>();
        var today = DateTime.Today;

        foreach (var month in report.Months)
        {
            var isCurrentMonth = month.Year == today.Year && month.Month == today.Month;
            var monthAnchor = Utils.GetCalendarReportMonthAnchor(month.Year, month.Month);
            sections.Add($"## {monthAnchor}{(isCurrentMonth ? " 🔵" : "")}");
            sections.Add("");

            var eventDays = month.Events.Select(e => e.Date.Day).ToHashSet();

            Utils.AppendCalendarGrid(
                sections,
                month.Year,
                month.Month,
                day =>
                {
                    var isToday = isCurrentMonth && day == today.Day;
                    return isToday
                        ? $"🔵 {day:00}"
                        : eventDays.Contains(day) ? $"📅 {day:00}" : $"⬜ {day:00}";
                });

            if (month.Month == frozenMonth)
            {
                var futureEvents = month.Events
                    .Where(e => e.Date.Day >= frozenBeforeDay)
                    .OrderBy(e => e.Date)
                    .ToList();
                var frozen = frozenEventLines ?? [];
                if (frozen.Count > 0 || futureEvents.Count > 0)
                {
                    sections.Add("");
                    sections.AddRange(frozen);
                    foreach (var evt in futureEvents)
                    {
                        var timeStr = evt.Date.TimeOfDay == TimeSpan.Zero
                            ? ""
                            : $" at {evt.Date:HH:mm}";
                        var eventText = $"{evt.Date.Day:00}{timeStr}: {evt.Note}";
                        var renderedEventText = evt.Cancelled ? $"~~{eventText}~~" : eventText;
                        sections.Add($"- {renderedEventText}");
                    }
                }
            }
            else if (month.Events.Count > 0)
            {
                sections.Add("");
                foreach (var evt in month.Events.OrderBy(e => e.Date))
                {
                    var timeStr = evt.Date.TimeOfDay == TimeSpan.Zero
                        ? ""
                        : $" at {evt.Date:HH:mm}";
                    var eventText = $"{evt.Date.Day:00}{timeStr}: {evt.Note}";
                    var renderedEventText = evt.Cancelled ? $"~~{eventText}~~" : eventText;
                    sections.Add($"- {renderedEventText}");
                }
            }

            sections.Add("");
        }

        return sections;
    }

    internal static Dictionary<int, List<string>> SplitReportIntoMonthSections(IEnumerable<string> lines)
    {
        var sections = new Dictionary<int, List<string>>();
        var currentMonth = 0;

        foreach (var line in lines)
        {
            if (line.StartsWith("## ", StringComparison.Ordinal) && line.Length >= 5
                && int.TryParse(line[3..5], out var m))
            {
                currentMonth = m;
                sections[currentMonth] = [line];
            }
            else if (currentMonth > 0)
            {
                sections[currentMonth].Add(line);
            }
        }

        return sections;
    }

    internal static List<string> ExtractPastEventLines(List<string> monthSection, int lastDayInclusive)
    {
        var result = new List<string>();
        foreach (var line in monthSection)
        {
            if (!line.StartsWith("- ", StringComparison.Ordinal))
                continue;

            var content = line[2..];
            if (content.StartsWith("~~", StringComparison.Ordinal))
                content = content[2..];

            if (content.Length >= 2 && char.IsDigit(content[0]) && char.IsDigit(content[1])
                && int.TryParse(content[..2], out var day) && day <= lastDayInclusive)
            {
                result.Add(line);
            }
        }
        return result;
    }
}
