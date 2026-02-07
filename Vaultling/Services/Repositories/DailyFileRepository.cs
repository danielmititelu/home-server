namespace Vaultling.Services.Repositories;

using Microsoft.Extensions.Options;
using Vaultling.Configuration;
using Vaultling.Models;

public class DailyFileRepository(IOptions<DailyFileOptions> options)
{
    private readonly DailyFileOptions _options = options.Value;

    public DailyFile ReadDailyFile()
    {
        var lines = File.ReadLines(_options.TodayFile);
        return DailyFile.Parse(lines);
    }

    public void ArchiveDailyFile(DateTime date)
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

    public void WriteDailyFile(DailyFile dailyFile)
    {
        File.WriteAllLines(_options.TodayFile, dailyFile.ToMarkdownLines());
    }
}
