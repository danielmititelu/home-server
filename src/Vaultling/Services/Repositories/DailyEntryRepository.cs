namespace Vaultling.Services.Repositories;

public class DailyEntryRepository(IOptions<DailyEntryOptions> options)
{
    private readonly DailyEntryOptions _options = options.Value;

    public DailyEntry ReadDailyEntry()
    {
        var lines = File.ReadLines(_options.TodayFile);
        return DailyEntry.Parse(lines);
    }

    public void ArchiveDailyFile(DateTimeOffset date)
    {
        var todayFilePath = _options.TodayFile;
        var archiveDir = _options.HistoryDirectory;
        var archiveFilePath = Path.Combine(archiveDir, $"daily-{date.ToIsoDateString()}.md");
        if (!Directory.Exists(archiveDir))
        {
            Directory.CreateDirectory(archiveDir);
        }

        File.Move(todayFilePath, archiveFilePath);
    }

    public void WriteDailyEntry(DailyEntry dailyEntry)
    {
        File.WriteAllLines(_options.TodayFile, dailyEntry.ToMarkdownLines());
    }
}
