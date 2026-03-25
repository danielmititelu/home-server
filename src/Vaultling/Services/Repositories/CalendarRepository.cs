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
            return ParseRecurringSchedule(schedule, note, cycleStart, cycleCount);
        }, maxColumnSplit: 4).ToList()
            : [];

        var recurringOccurrences = recurringEvents
                .SelectMany(e => GetOccurrences(
                    e,
                    new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero),
                    new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero)));

        var singleEventsFile = Utils.ResolveYearPath(_options.SingleEventsFile, year);
        var singleOccurrences =
            !string.IsNullOrEmpty(singleEventsFile) && File.Exists(singleEventsFile)
            ? Utils.ParseCsv(File.ReadLines(singleEventsFile), parts => ParseSingleOccurrence(parts, year), maxColumnSplit: 3)
            : [];

        var merged = MergeRecurringAndSingleOccurrences(recurringOccurrences, singleOccurrences);
        return ApplyCycleNumbering(merged, recurringEvents)
            .OrderBy(o => o.Date);
    }

    public void WriteCalendarReport(int year, IEnumerable<string> markdownLines)
    {
        var reportFile = Utils.ResolveYearPath(_options.ReportFile, year);
        if (string.IsNullOrEmpty(reportFile))
            return;

        File.WriteAllLines(reportFile, markdownLines);
    }

    private static CalendarOccurrence ParseSingleOccurrence(string[] parts, int year)
    {
        var datePart = parts[0].Trim();
        var note = parts[1].Trim();
        var cancelled = parts.Length > 2 && bool.TryParse(parts[2].Trim(), out var parsed) && parsed;
        var date = DateTime.Parse(
            $"{year}-{datePart}",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None);
        return new CalendarOccurrence(date, note, cancelled);
    }

    internal static IEnumerable<CalendarOccurrence> GetOccurrences(RecurringEvent recurring, DateTimeOffset from, DateTimeOffset to)
    {
        var type = recurring.Type.ToLowerInvariant();

        if (type == "monthly")
            return GetMonthlyOccurrences(recurring, from, to);

        if (Array.Exists(MonthNames, m => m == type))
            return GetYearlyOccurrences(recurring, from, to);

        if (Array.Exists(DayNames, d => d == type))
            return GetWeeklyOccurrences(recurring, from, to);

        return [];
    }

    private static RecurringEvent ParseRecurringSchedule(string schedule, string note, DateTime? cycleStart, int? cycleCount)
    {
        var atIndex = schedule.IndexOf(" at ", StringComparison.Ordinal);
        if (atIndex >= 0)
        {
            var dayName = schedule[..atIndex].Trim();
            var time = schedule[(atIndex + 4)..].Trim();
            if (Array.Exists(DayNames, d => d == dayName))
                return new RecurringEvent(Type: dayName, Schedule: time, Note: note, CycleStart: cycleStart, CycleCount: cycleCount);
        }

        if (schedule.StartsWith("monthly ", StringComparison.Ordinal))
            return new RecurringEvent(Type: "monthly", Schedule: schedule[8..].Trim(), Note: note, CycleStart: cycleStart, CycleCount: cycleCount);

        if (schedule.Length >= 5 && schedule[2] == '-')
        {
            var month = int.Parse(schedule[..2]);
            var day = schedule[3..];
            return new RecurringEvent(Type: MonthNames[month - 1], Schedule: day, Note: note, CycleStart: cycleStart, CycleCount: cycleCount);
        }

        return new RecurringEvent(Type: schedule, Schedule: "", Note: note, CycleStart: cycleStart, CycleCount: cycleCount);
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

    private static IEnumerable<CalendarOccurrence> MergeRecurringAndSingleOccurrences(
        IEnumerable<CalendarOccurrence> recurringOccurrences,
        IEnumerable<CalendarOccurrence> singleOccurrences)
    {
        // Single events are explicit overrides for the same date+note recurring entry.
        // This supports cases like "cancel recurring Thursday" + "add rescheduled Saturday".
        return recurringOccurrences
            .Concat(singleOccurrences)
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
