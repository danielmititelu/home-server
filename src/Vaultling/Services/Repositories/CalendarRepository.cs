namespace Vaultling.Services.Repositories;

using System.Globalization;

public class CalendarRepository(IOptions<CalendarOptions> options)
{
    private readonly CalendarOptions _options = options.Value;

    public void MaterializeRecurringEvents(int year)
    {
        var recurringFile = _options.EventsFile;
        var singleFile = ResolveYearPath(_options.SingleEventsFile, year);

        if (string.IsNullOrEmpty(recurringFile) || !File.Exists(recurringFile))
            return;

        var recurring = RecurringEvent.Parse(File.ReadLines(recurringFile));
        var from = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero);

        var allOccurrences = recurring
            .SelectMany(e => e.GetOccurrences(from, to))
            .ToList();

        var existingNotes = ReadExistingNotes(singleFile);

        var newLines = allOccurrences
            .Where(o => !existingNotes.Contains(o.Note))
            .Select(o => o.ToCsvLine())
            .ToList();

        if (newLines.Count == 0)
            return;

        if (!File.Exists(singleFile))
            File.WriteAllText(singleFile, "date,note\n");

        File.AppendAllLines(singleFile, newLines);
    }

    public IEnumerable<CalendarOccurrence> ReadCalendarOccurrences(int year)
    {
        var singleFile = ResolveYearPath(_options.SingleEventsFile, year);
        if (string.IsNullOrEmpty(singleFile) || !File.Exists(singleFile))
            return [];

        return CalendarEvent.Parse(File.ReadLines(singleFile))
            .Select(e => e.ToOccurrence(year));
    }

    public void WriteCalendarReport(int year, CalendarReport report)
    {
        var reportFile = ResolveYearPath(_options.ReportFile, year);
        if (string.IsNullOrEmpty(reportFile))
            return;

        File.WriteAllLines(reportFile, report.ToMarkdownLines());
    }

    private static string ResolveYearPath(string pathTemplate, int year)
    {
        if (string.IsNullOrEmpty(pathTemplate))
            return "";

        return pathTemplate.Replace("{year}", year.ToString(CultureInfo.InvariantCulture));
    }

    private static HashSet<string> ReadExistingNotes(string path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return [];

        return CalendarEvent.Parse(File.ReadLines(path))
            .Select(e => e.Note)
            .ToHashSet();
    }
}
