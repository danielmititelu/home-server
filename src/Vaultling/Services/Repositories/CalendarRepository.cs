namespace Vaultling.Services.Repositories;

using System.Globalization;
using System.Text.RegularExpressions;
using Vaultling.Utils;

public partial class CalendarRepository(IOptions<CalendarOptions> options, ExpenseRepository expenseRepository)
{
    [GeneratedRegex(@"\b(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2})?)\b", RegexOptions.Compiled)]
    private static partial Regex DateInDescriptionRegex();

    [GeneratedRegex(@"\b(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2})?)\b\s*->\s*\b(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2})?)\b", RegexOptions.Compiled)]
    private static partial Regex RangeDateInDescriptionRegex();

    [GeneratedRegex(@"\s+\b(?:pe|at|on|in|la|spre|to)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "")]
    private static partial Regex ConnectorWordRegex();

    [GeneratedRegex(@" \d+/\d+$")]
    private static partial Regex CycleNumberRegex();

    private readonly CalendarOptions _options = options.Value;
    private readonly ExpenseRepository _expenseRepository = expenseRepository;

    private static readonly Dictionary<string, DayOfWeek> DayNameToWeekday =
        Enum.GetValues<DayOfWeek>().ToDictionary(
            d => d.ToString().ToLowerInvariant(),
            d => d);

    public IEnumerable<CalendarOccurrence> ReadCalendarOccurrences(int year)
    {
        var recurringFile = _options.EventsFile;
        var recurringEvents =
            !string.IsNullOrEmpty(recurringFile) && File.Exists(recurringFile)
            ? Utils.ParseCsv(File.ReadLines(recurringFile), parts =>
            {
                var schedule = parts[0].Trim().ToLowerInvariant();
                var note = parts[1].Trim();
                var cycleCount = parts.Length > 2 && int.TryParse(parts[2].Trim(), out var cc) ? cc : (int?)null;
                string? cycleExpenseCategory = null, cycleExpenseDesc = null;
                if (parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]))
                {
                    var raw = parts[3].Trim();
                    var colonIdx = raw.IndexOf(':', StringComparison.Ordinal);
                    cycleExpenseCategory = colonIdx >= 0 ? raw[..colonIdx].Trim() : raw;
                    cycleExpenseDesc = colonIdx >= 0 ? raw[(colonIdx + 1)..].Trim() : "";
                }
                return ParseRecurringSchedule(schedule, note, cycleCount, cycleExpenseCategory, cycleExpenseDesc);
            }, maxColumnSplit: 4).OfType<RecurringEvent>().ToList()
            : [];

        var regularOccurrences = recurringEvents
            .OfType<YearlyRecurringEvent>()
            .Select(e => new CalendarOccurrence(new DateTime(year, e.Month, e.Day), e.Note));

        var cycleOccurrences = recurringEvents
            .OfType<WeeklyRecurringEvent>()
            .Where(e => e.CycleCount.HasValue && e.CycleExpenseCategory != null)
            .Select(e => (Event: e, Category: e.CycleExpenseCategory!, Desc: e.CycleExpenseDesc ?? ""))
            .SelectMany(t =>
            {
                var expenseDate = FindLatestMatchingExpense(t.Category, t.Desc, year);
                if (expenseDate == null) return [];
                return GetCycleOccurrences(t.Event, expenseDate.Value)
                    .Where(o => o.Date.Year == year);
            });

        var expenseEventOccurrences = ReadExpenseEvents(year);
        var reportOccurrences = ReadReportOccurrences(year);

        return MergeOccurrences(regularOccurrences.Concat(cycleOccurrences).Concat(expenseEventOccurrences), reportOccurrences)
            .OrderBy(o => o.Date);
    }

    internal IEnumerable<CalendarOccurrence> ReadExpenseEvents(int year)
    {
        foreach (var expense in _expenseRepository.ReadRecentExpenses())
        {
            var description = expense.Description.Trim();

            // Range match (departure -> return) takes priority
            var rangeMatch = RangeDateInDescriptionRegex().Match(description);
            if (rangeMatch.Success)
            {
                var notePart = ExtractNote(description, rangeMatch.Index);
                if (string.IsNullOrEmpty(notePart)) continue;

                if (DateTime.TryParse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var depDate)
                    && depDate.Year == year)
                    yield return new CalendarOccurrence(depDate, notePart);

                if (DateTime.TryParse(rangeMatch.Groups[2].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var retDate)
                    && retDate.Year == year)
                    yield return new CalendarOccurrence(retDate, notePart);

                continue;
            }

            // Single date
            var match = DateInDescriptionRegex().Match(description);
            if (!match.Success) continue;

            var dateStr = match.Groups[1].Value;
            var hasTime = dateStr.Length > 10;
            if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var eventDate))
                continue;

            if (eventDate.Year != year) continue;

            var note = ExtractNote(description, match.Index);
            if (string.IsNullOrEmpty(note)) continue;

            yield return new CalendarOccurrence(hasTime ? eventDate : eventDate.Date, note);
        }
    }

    internal string? GetTravelCityForDate(DateTime date)
    {
        foreach (var expense in _expenseRepository.ReadRecentExpenses())
        {
            var description = expense.Description.Trim();

            var rangeMatch = RangeDateInDescriptionRegex().Match(description);
            if (!rangeMatch.Success) continue;

            if (!DateTime.TryParse(rangeMatch.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var depDate))
                continue;
            if (!DateTime.TryParse(rangeMatch.Groups[2].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var retDate))
                continue;

            if (date.Date < depDate.Date || date.Date >= retDate.Date) continue;

            var notePart = ExtractNote(description, rangeMatch.Index);
            if (string.IsNullOrEmpty(notePart)) continue;

            var words = notePart.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) continue;

            return words[^1];
        }

        return null;
    }

    private static string ExtractNote(string description, int endIndex)
    {
        var notePart = description[..endIndex];
        return ConnectorWordRegex().Replace(notePart, "").Trim();
    }

    internal DateTime? FindLatestMatchingExpense(string category, string desc, int year)
    {
        var expense = _expenseRepository.FindLatestExpense(category, desc);
        return expense == null ? null : new DateTime(year, expense.Month, expense.Day);
    }

    internal static IEnumerable<CalendarOccurrence> GetCycleOccurrences(WeeklyRecurringEvent weekly, DateTime expenseDate)
    {
        var count = weekly.CycleCount!.Value;
        var current = expenseDate.Date;
        while (current.DayOfWeek != weekly.Day)
            current = current.AddDays(1);

        for (var i = 0; i <= count; i++)
        {
            var number = (i == count) ? 1 : i + 1; // last one is the speculative 1/N
            yield return new CalendarOccurrence(current.Add(weekly.Time.ToTimeSpan()), $"{weekly.Note} {number}/{count}");
            current = current.AddDays(7);
        }
    }

    internal IEnumerable<CalendarOccurrence> ReadReportOccurrences(int year)
    {
        var reportFile = Utils.ResolveYearPath(_options.ReportFile, year);
        if (string.IsNullOrEmpty(reportFile) || !File.Exists(reportFile))
            return [];

        var result = new List<CalendarOccurrence>();
        var currentMonth = 0;

        foreach (var line in File.ReadLines(reportFile))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal) && line.Length >= 5
                && int.TryParse(line[3..5], out var m))
            {
                currentMonth = m;
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal) && currentMonth > 0)
            {
                var occurrence = ParseReportEventLine(line, year, currentMonth);
                if (occurrence != null)
                    result.Add(occurrence);
            }
        }

        return result;
    }

    internal static CalendarOccurrence? ParseReportEventLine(string line, int year, int month)
    {
        var content = line[2..].Trim();
        var cancelled = content.StartsWith("~~", StringComparison.Ordinal)
                     && content.EndsWith("~~", StringComparison.Ordinal);
        if (cancelled)
            content = content[2..^2];

        // Event lines start with a day number: "DD[ at HH:MM]: Note"
        if (content.Length < 3 || !char.IsDigit(content[0]))
            return null;

        var colonIndex = content.IndexOf(": ", StringComparison.Ordinal);
        if (colonIndex < 0)
            return null;

        var datePart = content[..colonIndex];
        var note = content[(colonIndex + 2)..];

        var day = int.Parse(datePart[..2]);
        var hour = 0;
        var minute = 0;

        var atIndex = datePart.IndexOf(" at ", StringComparison.Ordinal);
        if (atIndex >= 0)
        {
            var timeParts = datePart[(atIndex + 4)..].Split(':');
            hour = int.Parse(timeParts[0]);
            minute = int.Parse(timeParts[1]);
        }

        return new CalendarOccurrence(new DateTime(year, month, day, hour, minute, 0), note, cancelled);
    }

    public IEnumerable<string> ReadCalendarReportLines(int year)
    {
        var reportFile = Utils.ResolveYearPath(_options.ReportFile, year);
        if (string.IsNullOrEmpty(reportFile) || !File.Exists(reportFile))
            return [];

        return File.ReadLines(reportFile);
    }

    public void WriteCalendarReport(int year, IEnumerable<string> markdownLines)
    {
        var reportFile = Utils.ResolveYearPath(_options.ReportFile, year);
        if (string.IsNullOrEmpty(reportFile))
            return;

        File.WriteAllLines(reportFile, markdownLines);
    }

    private static RecurringEvent? ParseRecurringSchedule(string schedule, string note, int? cycleCount = null, string? cycleExpenseCategory = null, string? cycleExpenseDesc = null)
    {
        return schedule switch
        {
            _ when schedule.Split(" at ", 2) is [var dayName, var time]
                && DayNameToWeekday.TryGetValue(dayName.Trim(), out var weekday) =>
                new WeeklyRecurringEvent(weekday, TimeOnly.Parse(time.Trim()), note, CycleCount: cycleCount, CycleExpenseCategory: cycleExpenseCategory, CycleExpenseDesc: cycleExpenseDesc),
            _ when schedule.Split('-') is [var monthStr, var dayStr]
                && int.TryParse(monthStr, out var monthNum)
                && monthNum >= 1 && monthNum <= 12
                && int.TryParse(dayStr, out var dayNum) =>
                new YearlyRecurringEvent(monthNum, dayNum, note),
            _ => LogAndReturnNull(schedule, note)
        };
    }

    private static RecurringEvent? LogAndReturnNull(string schedule, string note)
    {
        Console.Error.WriteLine($"[CalendarRepository] Unrecognised schedule format — schedule: '{schedule}', note: '{note}'");
        return null;
    }

    private static IEnumerable<CalendarOccurrence> MergeOccurrences(
        IEnumerable<CalendarOccurrence> recurringOccurrences,
        IEnumerable<CalendarOccurrence> reportOccurrences)
    {
        // Report events override recurring events matched by date + base note (stripping X/N suffix).
        // Strikethrough entries in the report cancel the matching recurring event.
        // Non-matching report events are user-added single events.
        return recurringOccurrences
            .Concat(reportOccurrences)
            .GroupBy(o => new
            {
                o.Date,
                Note = CycleNumberRegex().Replace(o.Note.Trim(), "").ToLowerInvariant()
            })
            .Select(g => g.FirstOrDefault(o => o.Cancelled) ?? g.Last());
    }
}
