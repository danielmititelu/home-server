namespace Vaultling.Services.Repositories;

using System.Globalization;
using Vaultling.Utils;

public class CalendarRepository(IOptions<CalendarOptions> options)
{
    private readonly CalendarOptions _options = options.Value;
    private static readonly string[] MonthNames = CultureInfo.InvariantCulture.DateTimeFormat.MonthNames
        .Where(m => !string.IsNullOrEmpty(m))
        .Select(m => m.ToLowerInvariant())
        .ToArray();

    private static readonly string[] DayNames = CultureInfo.InvariantCulture.DateTimeFormat.DayNames
        .Select(d => d.ToLowerInvariant())
        .ToArray();

    public IEnumerable<CalendarOccurrence> ReadCalendarOccurrences(int year)
    {
        var recurringFile = _options.EventsFile;
        var recurringEvents =
            !string.IsNullOrEmpty(recurringFile) && File.Exists(recurringFile)
            ? Utils.ParseCsv(File.ReadLines(recurringFile), parts =>
        {
            var schedule = parts[0].Trim().ToLowerInvariant();
            var note = parts[1].Trim();
            var cycleStart = parts.Length > 2 && DateTime.TryParse(parts[2].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var cs) ? cs : (DateTime?)null;
            var cycleCount = parts.Length > 3 && int.TryParse(parts[3].Trim(), out var cc) ? cc : (int?)null;
            var cycleEnd = parts.Length > 4 && DateTime.TryParse(parts[4].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var ce) ? ce : (DateTime?)null;
            return ParseRecurringSchedule(schedule, note, cycleStart, cycleCount, cycleEnd);
        }, maxColumnSplit: 5).ToList()
            : [];

        var recurringOccurrences = recurringEvents
                .SelectMany(e => GetOccurrences(
                    e,
                    new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero)));

        var numberedOccurrences = ApplyCycleNumbering(recurringOccurrences, recurringEvents);
        var reportOccurrences = ReadReportOccurrences(year);

        return MergeOccurrences(numberedOccurrences, reportOccurrences)
            .OrderBy(o => o.Date);
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
        if (recurring.CycleEnd.HasValue)
        {
            var cycleEndOffset = new DateTimeOffset(recurring.CycleEnd.Value, TimeSpan.Zero);
            if (cycleEndOffset < to)
                to = cycleEndOffset;
        }

        var type = recurring.Type.ToLowerInvariant();

        if (type == "monthly")
            return GetMonthlyOccurrences(recurring, from, to);

        if (Array.Exists(MonthNames, m => m == type))
            return GetYearlyOccurrences(recurring, from, to);

        if (Array.Exists(DayNames, d => d == type))
            return GetWeeklyOccurrences(recurring, from, to);

        return [];
    }

    private static RecurringEvent ParseRecurringSchedule(string schedule, string note, DateTime? cycleStart, int? cycleCount, DateTime? cycleEnd = null)
    {
        var atIndex = schedule.IndexOf(" at ", StringComparison.Ordinal);
        if (atIndex >= 0)
        {
            var dayName = schedule[..atIndex].Trim();
            var time = schedule[(atIndex + 4)..].Trim();
            if (Array.Exists(DayNames, d => d == dayName))
                return new RecurringEvent(Type: dayName, Schedule: time, Note: note, CycleStart: cycleStart, CycleCount: cycleCount, CycleEnd: cycleEnd);
        }

        if (schedule.StartsWith("monthly ", StringComparison.Ordinal))
            return new RecurringEvent(Type: "monthly", Schedule: schedule[8..].Trim(), Note: note, CycleStart: cycleStart, CycleCount: cycleCount, CycleEnd: cycleEnd);

        if (schedule.Length >= 5 && schedule[2] == '-')
        {
            var month = int.Parse(schedule[..2]);
            var day = schedule[3..];
            return new RecurringEvent(Type: MonthNames[month - 1], Schedule: day, Note: note, CycleStart: cycleStart, CycleCount: cycleCount, CycleEnd: cycleEnd);
        }

        return new RecurringEvent(Type: schedule, Schedule: "", Note: note, CycleStart: cycleStart, CycleCount: cycleCount, CycleEnd: cycleEnd);
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

    private static IEnumerable<CalendarOccurrence> GetMonthlyOccurrences(RecurringEvent recurring, DateTimeOffset from, DateTimeOffset to)
    {
        var day = int.Parse(recurring.Schedule);

        var current = new DateTime(from.Year, from.Month, 1);
        var end = new DateTime(to.Year, to.Month, 1).AddMonths(1);

        while (current < end)
        {
            var daysInMonth = DateTime.DaysInMonth(current.Year, current.Month);
            if (day <= daysInMonth)
            {
                var dt = new DateTime(current.Year, current.Month, day);
                if (dt >= from.Date && dt <= to.Date)
                    yield return new CalendarOccurrence(dt, recurring.Note);
            }

            current = current.AddMonths(1);
        }
    }

    private static IEnumerable<CalendarOccurrence> MergeOccurrences(
        IEnumerable<CalendarOccurrence> recurringOccurrences,
        IEnumerable<CalendarOccurrence> reportOccurrences)
    {
        // Report events override recurring events with the same date+note.
        // Strikethrough entries in the report cancel the matching recurring event.
        // Non-matching report events are user-added single events.
        return recurringOccurrences
            .Concat(reportOccurrences)
            .GroupBy(o => new
            {
                o.Date,
                Note = o.Note.Trim().ToLowerInvariant()
            })
            .Select(g => g.FirstOrDefault(o => o.Cancelled) ?? g.First());
    }

    internal static IEnumerable<CalendarOccurrence> ApplyCycleNumbering(
        IEnumerable<CalendarOccurrence> occurrences,
        IEnumerable<RecurringEvent> recurringEvents)
    {
        var cycleRules = recurringEvents
            .Where(e => e.CycleStart.HasValue && e.CycleCount.HasValue)
            .ToDictionary(
                e => e.Note.Trim().ToLowerInvariant(),
                e => (Start: e.CycleStart!.Value, Count: e.CycleCount!.Value));

        if (cycleRules.Count == 0)
            return occurrences;

        var result = occurrences.OrderBy(o => o.Date).ToList();

        // For each cycle rule, number matching occurrences from cycle-start onward
        foreach (var (noteKey, rule) in cycleRules)
        {
            var counter = 1;
            for (var i = 0; i < result.Count; i++)
            {
                var o = result[i];
                if (!o.Note.Trim().Equals(noteKey, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (o.Date < rule.Start)
                    continue;

                result[i] = o with { Note = $"{o.Note} {counter}/{rule.Count}" };
                counter = counter % rule.Count + 1;
            }
        }

        return result;
    }
}
