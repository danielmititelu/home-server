namespace Vaultling.Services.Repositories;

public class DailyEntryRepository(IOptions<DailyEntryOptions> options)
{
    private readonly DailyEntryOptions _options = options.Value;

    public DailyEntry ReadDailyEntry()
    {
        var lines = File.ReadLines(_options.TodayFile);
        return ParseDailyEntry(lines);
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

    public static DailyEntry ParseDailyEntry(IEnumerable<string> lines)
    {
        var sectionsContent = new Dictionary<string, List<string>>();
        string? currentSection = null;

        foreach (var line in lines)
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
        var workouts = ParseDailyWorkouts(sectionsContent[DailySectionName.Workout.ToString()]);
        var expenses = ParseDailyExpenses(sectionsContent[DailySectionName.Expenses.ToString()]);
        var todos = sectionsContent[DailySectionName.Todo.ToString()];

        return new DailyEntry(date, workouts, todos, expenses, []);
    }

    private static IEnumerable<DailyWorkout> ParseDailyWorkouts(List<string> csvLines)
    {
        return csvLines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new DailyWorkout(
                    Exercise: parts[0],
                    Reps: parts.Length > 1 ? parts[1] : ""
                );
            });
    }

    private static IEnumerable<DailyExpense> ParseDailyExpenses(List<string> csvLines)
    {
        return csvLines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new DailyExpense(
                    Category: parts[0],
                    Amount: parts.Length > 1 && decimal.TryParse(parts[1], out var amt) ? amt : 0,
                    Description: parts.Length > 2 ? parts[2] : ""
                );
            });
    }
}
