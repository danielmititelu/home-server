namespace Vaultling.Services.Repositories;

using System.Globalization;
using System.Text.RegularExpressions;
using Vaultling.Utils;

public partial class CalendarRepository(IOptions<CalendarOptions> options)
{
    [GeneratedRegex(@"\b(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2})?)\b", RegexOptions.Compiled)]
    private static partial Regex DateInDescriptionRegex();

    [GeneratedRegex(@"\b(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2})?)\b\s*->\s*\b(\d{4}-\d{2}-\d{2}(?:T\d{2}:\d{2})?)\b", RegexOptions.Compiled)]
    private static partial Regex RangeDateInDescriptionRegex();

    [GeneratedRegex(@"\s+\b(?:pe|at|on|in|la|spre|to)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled, "")]
    private static partial Regex ConnectorWordRegex();

    private readonly CalendarOptions _options = options.Value;
    private static readonly string[] MonthNames = [.. CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
        .Where(m => !string.IsNullOrEmpty(m))
        .Select(m => m.ToLowerInvariant())];

    private static readonly string[] DayNames = [.. CultureInfo.InvariantCulture.DateTimeFormat.DayNames
        .Select(d => d.ToLowerInvariant())];

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
                var cycleExpenseMatch = parts.Length > 3 && !string.IsNullOrWhiteSpace(parts[3]) ? parts[3].Trim() : null;
                return ParseRecurringSchedule(schedule, note, cycleCount, cycleExpenseMatch);
            }, maxColumnSplit: 4).ToList()
            : [];

        var yearFrom = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var yearTo = new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var regularEvents = recurringEvents.Where(e => e.CycleCount == null || e.CycleExpenseMatch == null);
        var cycleEvents = recurringEvents.Where(e => e.CycleCount.HasValue && e.CycleExpenseMatch != null);

        var regularOccurrences = regularEvents
            .SelectMany(e => GetOccurrences(e, yearFrom, yearTo));

        var cycleOccurrences = cycleEvents
            .SelectMany(e =>
            {
                var expenseDate = FindLatestMatchingExpense(e.CycleExpenseMatch!, year);
                if (expenseDate == null) return [];
                return GetCycleOccurrences(e, expenseDate.Value)
                    .Where(o => o.Date.Year == year);
            });

        var expenseEventOccurrences = ReadExpenseEvents(year);
        var reportOccurrences = ReadReportOccurrences(year);

        return MergeOccurrences(regularOccurrences.Concat(cycleOccurrences).Concat(expenseEventOccurrences), reportOccurrences)
            .OrderBy(o => o.Date);
    }

    internal IEnumerable<CalendarOccurrence> ReadExpenseEvents(int year)
    {
        foreach (var checkYear in new[] { year - 1, year })
        {
            var expenseFile = Utils.ResolveYearPath(_options.ExpenseDataFile, checkYear);
            if (string.IsNullOrEmpty(expenseFile) || !File.Exists(expenseFile))
                continue;

            foreach (var parts in Utils.ParseCsv(File.ReadLines(expenseFile),
                p => p, maxColumnSplit: 5))
            {
                if (parts.Length < 5) continue;
                var description = parts[4].Trim();

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
    }

    internal string? GetTravelCityForDate(DateTime date, int year)
    {
        foreach (var checkYear in new[] { year - 1, year })
        {
            var expenseFile = Utils.ResolveYearPath(_options.ExpenseDataFile, checkYear);
            if (string.IsNullOrEmpty(expenseFile) || !File.Exists(expenseFile))
                continue;

            foreach (var parts in Utils.ParseCsv(File.ReadLines(expenseFile),
                p => p, maxColumnSplit: 5))
            {
                if (parts.Length < 5) continue;
                var description = parts[4].Trim();

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
        }

        return null;
    }

    private static string ExtractNote(string description, int endIndex)
    {
        var notePart = description[..endIndex];
        return ConnectorWordRegex().Replace(notePart, "").Trim();
    }

    internal DateTime? FindLatestMatchingExpense(string cycleExpenseMatch, int year)
    {
        var dates = new List<DateTime>();
        foreach (var checkYear in new[] { year - 1, year })
        {
            var expenseFile = Utils.ResolveYearPath(_options.ExpenseDataFile, checkYear);
            if (string.IsNullOrEmpty(expenseFile) || !File.Exists(expenseFile))
                continue;

            dates.AddRange(ReadMatchingExpenseDates(expenseFile, cycleExpenseMatch, checkYear));
        }

        return dates.Count > 0 ? dates.Max() : null;
    }

    private static IEnumerable<DateTime> ReadMatchingExpenseDates(string expenseFile, string cycleExpenseMatch, int year)
    {
        var colonIdx = cycleExpenseMatch.IndexOf(':', StringComparison.Ordinal);
        var categoryMatch = colonIdx >= 0 ? cycleExpenseMatch[..colonIdx].Trim() : cycleExpenseMatch.Trim();
        var descMatch = colonIdx >= 0 ? cycleExpenseMatch[(colonIdx + 1)..].Trim() : "";

        return Utils.ParseCsv(File.ReadLines(expenseFile), parts =>
        {
            if (parts.Length < 5) return (DateTime?)null;
            if (!int.TryParse(parts[0].Trim(), out var month) || !int.TryParse(parts[1].Trim(), out var day))
                return (DateTime?)null;

            var category = parts[2].Trim();
            var description = parts[4].Trim();

            var categoryOk = string.IsNullOrEmpty(categoryMatch) ||
                category.Contains(categoryMatch, StringComparison.OrdinalIgnoreCase);
            var descOk = string.IsNullOrEmpty(descMatch) ||
                description.Contains(descMatch, StringComparison.OrdinalIgnoreCase);

            return (categoryOk && descOk) ? new DateTime(year, month, day) : (DateTime?)null;
        }, maxColumnSplit: 5).OfType<DateTime>();
    }

    internal static IEnumerable<CalendarOccurrence> GetCycleOccurrences(RecurringEvent recurring, DateTime expenseDate)
    {
        var count = recurring.CycleCount!.Value;
        var from = new DateTimeOffset(expenseDate, TimeSpan.Zero);
        var farFuture = new DateTimeOffset(expenseDate.Year + 2, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var occurrences = GetOccurrences(recurring, from, farFuture).Take(count + 1).ToList();

        for (var i = 0; i < occurrences.Count; i++)
        {
            var number = (i == count) ? 1 : i + 1; // last one is the speculative 1/N
            yield return occurrences[i] with { Note = $"{occurrences[i].Note} {number}/{count}" };
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

    internal static IEnumerable<CalendarOccurrence> GetOccurrences(RecurringEvent recurring, DateTimeOffset from, DateTimeOffset to)
    {
        var type = recurring.Type.ToLowerInvariant();

        if (Array.Exists(MonthNames, m => m == type))
            return GetYearlyOccurrences(recurring, from, to);

        if (Array.Exists(DayNames, d => d == type))
            return GetWeeklyOccurrences(recurring, from, to);

        return [];
    }

    private static RecurringEvent ParseRecurringSchedule(string schedule, string note, int? cycleCount = null, string? cycleExpenseMatch = null)
    {
        var atIndex = schedule.IndexOf(" at ", StringComparison.Ordinal);
        if (atIndex >= 0)
        {
            var dayName = schedule[..atIndex].Trim();
            var time = schedule[(atIndex + 4)..].Trim();
            if (Array.Exists(DayNames, d => d == dayName))
                return new RecurringEvent(Type: dayName, Schedule: time, Note: note, CycleCount: cycleCount, CycleExpenseMatch: cycleExpenseMatch);
        }

        if (schedule.Length >= 5 && schedule[2] == '-')
        {
            var month = int.Parse(schedule[..2]);
            var day = schedule[3..];
            return new RecurringEvent(Type: MonthNames[month - 1], Schedule: day, Note: note, CycleCount: cycleCount, CycleExpenseMatch: cycleExpenseMatch);
        }

        return new RecurringEvent(Type: schedule, Schedule: "", Note: note, CycleCount: cycleCount, CycleExpenseMatch: cycleExpenseMatch);
    }

    private static IEnumerable<CalendarOccurrence> GetYearlyOccurrences(RecurringEvent recurring, DateTimeOffset from, DateTimeOffset to)
    {
        var monthIndex = Array.FindIndex(MonthNames, m => m.Equals(recurring.Type, StringComparison.InvariantCultureIgnoreCase)) + 1;
        var day = int.Parse(recurring.Schedule);

        for (var year = from.Year; year <= to.Year; year++)
        {
            var dt = new DateTime(year, monthIndex, day);
            if (dt >= from.Date && dt <= to.Date)
                yield return new CalendarOccurrence(dt, recurring.Note);
        }
    }

    private static IEnumerable<CalendarOccurrence> GetWeeklyOccurrences(RecurringEvent recurring, DateTimeOffset from, DateTimeOffset to)
    {
        var targetDay = (DayOfWeek)Array.FindIndex(DayNames, d => d.Equals(recurring.Type, StringComparison.InvariantCultureIgnoreCase));
        var timeParts = recurring.Schedule.Split(':');
        var hour = int.Parse(timeParts[0]);
        var minute = int.Parse(timeParts[1]);

        var current = from.Date;
        while (current <= to.Date)
        {
            if (current.DayOfWeek == targetDay)
                yield return new CalendarOccurrence(current.Add(new TimeSpan(hour, minute, 0)), recurring.Note);
            current = current.AddDays(1);
        }
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
                Note = StripCycleNumber(o.Note).ToLowerInvariant()
            })
            .Select(g => g.FirstOrDefault(o => o.Cancelled) ?? g.Last());
    }

    internal static string StripCycleNumber(string note)
    {
        return Regex.Replace(note.Trim(), @" \d+/\d+$", "");
    }
}
