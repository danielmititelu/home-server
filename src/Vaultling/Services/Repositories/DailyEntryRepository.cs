namespace Vaultling.Services.Repositories;

using Vaultling.Utils;

public class DailyEntryRepository(IOptions<DailyEntryOptions> options)
{
    private readonly DailyEntryOptions _options = options.Value;

    public DailyEntry ReadDailyEntry()
    {
        var sectionsContent = new Dictionary<string, List<string>>();
        string? currentSection = null;

        foreach (var line in File.ReadLines(_options.TodayFile))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                currentSection = line[2..].Trim();
                sectionsContent[currentSection] = [];
            }
            else if (!string.IsNullOrWhiteSpace(line) && currentSection != null)
            {
                sectionsContent[currentSection].Add(line.Trim());
            }
        }

        var date = DateTimeOffset.Parse(sectionsContent[DailySectionName.Date.ToString()].First());
        var workouts = Utils.ParseCsv(sectionsContent[DailySectionName.Workout.ToString()], parts => new DailyWorkout(
            Exercise: parts[0],
            Reps: parts.Length > 1 ? parts[1] : ""
        ));
        var expenses = Utils.ParseCsv(sectionsContent[DailySectionName.Expenses.ToString()], parts => new DailyExpense(
            Category: parts[0],
            Amount: parts.Length > 1 && decimal.TryParse(parts[1], out var amt) ? amt : 0,
            Description: parts.Length > 2 ? parts[2] : ""
        ));
        var todos = sectionsContent[DailySectionName.Todo.ToString()];

        var weatherSection = sectionsContent.TryGetValue(DailySectionName.Weather.ToString(), out var weatherLines)
            ? weatherLines
            : [];
        var city = weatherSection.FirstOrDefault() ?? "";

        return new DailyEntry(date, workouts, todos, expenses, [], City: city);
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

    public void WriteDailyEntry(IEnumerable<string> markdownLines)
    {
        File.WriteAllLines(_options.TodayFile, markdownLines);
    }
}
