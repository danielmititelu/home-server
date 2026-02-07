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
        var sectionsContent = new Dictionary<string, List<string>>();
        string? currentSection = null;

        foreach (var line in lines)
        {
            if (line.StartsWith("# "))
            {
                currentSection = line[2..].Trim();
                sectionsContent[currentSection] = [];
            }
            else if (!string.IsNullOrWhiteSpace(line) && currentSection != null)
            {
                sectionsContent[currentSection].Add(line.Trim());
            }
        }

        var dateSection = DateTime.Parse(sectionsContent[DailySectionName.Date.ToString()].First());

        var workouts = sectionsContent[DailySectionName.Workout.ToString()].ParseDailyWorkouts();
        var expenses = sectionsContent[DailySectionName.Expenses.ToString()].ParseDailyExpenses();

        return new DailyFile(dateSection, workouts, expenses);
    }

    public void ArchiveDailyFile(DateTime date)
    {
        var todayFilePath = _options.TodayFile;
        var archiveDir = _options.HistoryDirectory;
        var archiveFilePath = Path.Combine(archiveDir, $"daily-{date:yyyy-MM-dd}.md");
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
