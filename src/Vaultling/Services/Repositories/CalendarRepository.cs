namespace Vaultling.Services.Repositories;

using System.Globalization;
using System.Text.RegularExpressions;
using Vaultling.Utils;

public partial class CalendarRepository(IOptions<CalendarOptions> options, ExpenseRepository expenseRepository, TimeProvider timeProvider)
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
    private readonly TimeProvider _timeProvider = timeProvider;

    private static readonly Dictionary<string, DayOfWeek> DayNameToWeekday =
        Enum.GetValues<DayOfWeek>().ToDictionary(
            d => d.ToString().ToLowerInvariant(),
            d => d);

    public IEnumerable<CalendarOccurrence> CollectCalendarOccurrencesForYear(int year)
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
                var latestExpense = _expenseRepository.FindLatestExpense(t.Category, t.Desc);
                var expenseDate = latestExpense == null
                    ? (DateTime?)null
                    : new DateTime(latestExpense.Year, latestExpense.Month, latestExpense.Day);
                if (expenseDate == null) return [];
                return GetCycleOccurrences(t.Event, expenseDate.Value)
                    .Where(o => o.Date.Year == year);
            });

        var expenseEventOccurrences = ReadExpenseEvents(year);
        var reportOccurrences = ReadReportOccurrences(year);

        var cycleBaseNotes = recurringEvents
            .OfType<WeeklyRecurringEvent>()
            .Where(e => e.CycleCount.HasValue && e.CycleExpenseCategory != null)
            .Select(e => e.Note.Trim().ToLowerInvariant())
            .ToHashSet();

        var today = _timeProvider.GetLocalNow().Date;

        return MergeOccurrences(
                regularOccurrences.Concat(cycleOccurrences).Concat(expenseEventOccurrences),
                reportOccurrences,
                cycleBaseNotes,
                today)
            .OrderBy(o => o.Date);
    }

    public string? GetTravelCityForDate(DateTime date)
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

    public void WriteCalendarReport(int year, string markdown)
    {
        var reportFile = Utils.ResolveYearPath(_options.ReportFileTemplate, year);
        if (string.IsNullOrEmpty(reportFile))
            return;

        File.WriteAllText(reportFile, markdown);
    }

    private IEnumerable<CalendarOccurrence> ReadExpenseEvents(int year)
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

    private static string ExtractNote(string description, int endIndex)
    {
        var notePart = description[..endIndex];
        return ConnectorWordRegex().Replace(notePart, "").Trim();
    }


    internal static IEnumerable<CalendarOccurrence> GetCycleOccurrences(WeeklyRecurringEvent weekly, DateTime expenseDate)
    {
        var count = weekly.CycleCount!.Value;
        var time = weekly.Time.ToTimeSpan();

        // 1/N: anchored on the expense day itself at the schedule time.
        yield return new CalendarOccurrence(expenseDate.Date.Add(time), $"{weekly.Note} 1/{count}");

        // 2/N..N/N + speculative 1/N: schedule weekday in the ISO week (Mon-Sun)
        // *after* the expense's ISO week, then weekly thereafter.
        var nextWeekStart = StartOfIsoWeek(expenseDate.Date).AddDays(7);
        var dayOffset = ((int)weekly.Day - (int)DayOfWeek.Monday + 7) % 7;
        var current = nextWeekStart.AddDays(dayOffset);

        for (var i = 2; i <= count; i++)
        {
            yield return new CalendarOccurrence(current.Add(time), $"{weekly.Note} {i}/{count}");
            current = current.AddDays(7);
        }

        // Speculative next-cycle 1/N
        yield return new CalendarOccurrence(current.Add(time), $"{weekly.Note} 1/{count}");
    }

    private static DateTime StartOfIsoWeek(DateTime d)
    {
        // ISO week starts on Monday.
        var offset = ((int)d.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return d.Date.AddDays(-offset);
    }

    internal IEnumerable<CalendarOccurrence> ReadReportOccurrences(int year)
    {
        var reportFile = Utils.ResolveYearPath(_options.ReportFileTemplate, year);
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

    private enum OccurrenceSource { Recurring, Report }

    private static string BaseNote(string note) =>
        CycleNumberRegex().Replace(note.Trim(), "").ToLowerInvariant();

    private static IEnumerable<CalendarOccurrence> MergeOccurrences(
        IEnumerable<CalendarOccurrence> recurringOccurrences,
        IEnumerable<CalendarOccurrence> reportOccurrences,
        HashSet<string> cycleBaseNotes,
        DateTime today)
    {
        // Tag every occurrence with its source so the merge can distinguish
        // freshly-computed entries from entries already written to the report.
        var tagged = recurringOccurrences
            .Select(o => (Source: OccurrenceSource.Recurring, Occ: o))
            .Concat(reportOccurrences.Select(o => (Source: OccurrenceSource.Report, Occ: o)))
            .GroupBy(x => BaseNote(x.Occ.Note));

        foreach (var byBase in tagged)
        {
            var baseNote = byBase.Key;
            var entries = byBase.ToList();

            if (!cycleBaseNotes.Contains(baseNote))
            {
                // Non-cycle: original behaviour. Cancellation in the bucket wins;
                // otherwise the report entry (concatenated last) wins.
                foreach (var dateGroup in entries.GroupBy(x => x.Occ.Date))
                {
                    var cancelled = dateGroup.FirstOrDefault(x => x.Occ.Cancelled);
                    yield return cancelled.Occ ?? dateGroup.Last().Occ;
                }
                continue;
            }

            // Cycle: detect manual-edit weeks (strike + non-strike of the same
            // base-note in the same ISO week — the user cancelled the canonical
            // entry and rescheduled within the week).
            var manualEditWeeks = entries
                .Where(x => x.Source == OccurrenceSource.Report)
                .GroupBy(x => StartOfIsoWeek(x.Occ.Date))
                .Where(g => g.Any(x => x.Occ.Cancelled) && g.Any(x => !x.Occ.Cancelled))
                .Select(g => g.Key)
                .ToHashSet();

            foreach (var weekGroup in entries.GroupBy(x => StartOfIsoWeek(x.Occ.Date)))
            {
                if (manualEditWeeks.Contains(weekGroup.Key))
                {
                    // Preserve everything the user wrote in the report; drop the
                    // freshly-computed recurring entries entirely for this week.
                    foreach (var x in weekGroup.Where(x => x.Source == OccurrenceSource.Report))
                        yield return x.Occ;
                    continue;
                }

                foreach (var dateGroup in weekGroup.GroupBy(x => x.Occ.Date))
                {
                    var cancelled = dateGroup.FirstOrDefault(x => x.Occ.Cancelled);
                    if (cancelled.Occ != null)
                    {
                        // Report cancellation wins (lone strike suppresses fresh entry).
                        yield return cancelled.Occ;
                        continue;
                    }

                    var hasReport = dateGroup.Any(x => x.Source == OccurrenceSource.Report);
                    var hasRecurring = dateGroup.Any(x => x.Source == OccurrenceSource.Recurring);

                    if (dateGroup.Key < today)
                    {
                        // Past is immutable: keep what the report said; if no report
                        // entry exists yet (first run) fall back to the fresh value.
                        if (hasReport)
                            yield return dateGroup.First(x => x.Source == OccurrenceSource.Report).Occ;
                        else if (hasRecurring)
                            yield return dateGroup.First(x => x.Source == OccurrenceSource.Recurring).Occ;
                    }
                    else
                    {
                        // Today/future: re-anchor — fresh recurring wins, stale
                        // future report entries with no fresh counterpart are dropped.
                        if (hasRecurring)
                            yield return dateGroup.First(x => x.Source == OccurrenceSource.Recurring).Occ;
                    }
                }
            }
        }
    }
}
