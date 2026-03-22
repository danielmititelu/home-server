namespace Vaultling.Services.Repositories;

using System.Globalization;

public class CalendarRepository(IOptions<CalendarOptions> options)
{
    private readonly CalendarOptions _options = options.Value;

    public IEnumerable<CalendarOccurrence> ReadCalendarOccurrences(int year)
    {
        var recurringFile = _options.EventsFile;
        if (string.IsNullOrEmpty(recurringFile) || !File.Exists(recurringFile))
            return [];

        var recurring = RecurringEvent.Parse(File.ReadLines(recurringFile));
        var from = new DateTimeOffset(year, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(year, 12, 31, 23, 59, 59, TimeSpan.Zero);

        return recurring.SelectMany(e => e.GetOccurrences(from, to));
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
}
