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

        if (!sectionsContent.TryGetValue(DailySectionName.Date.ToString(), out var dateLines)
            || dateLines.Count == 0
            || !DateTimeOffset.TryParse(dateLines[0], out var date))
            throw new InvalidOperationException($"Daily entry file '{_options.TodayFile}' is missing a valid '{DailySectionName.Date}' section.");

        if (!sectionsContent.TryGetValue(DailySectionName.Workout.ToString(), out var workoutLines))
            workoutLines = [];
        if (!sectionsContent.TryGetValue(DailySectionName.Expenses.ToString(), out var expenseLines))
            expenseLines = [];
        if (!sectionsContent.TryGetValue(DailySectionName.Todo.ToString(), out var todoLines))
            todoLines = [];

        var workouts = Utils.ParseCsv(workoutLines, parts => new DailyWorkout(
            Exercise: parts[0],
            Reps: parts.Length > 1 ? parts[1] : ""
        ), maxColumnSplit: 2);
        var expenses = Utils.ParseCsv(expenseLines, parts => new DailyExpense(
            Category: parts[0],
            Amount: parts.Length > 1 && decimal.TryParse(parts[1], out var amt) ? amt : 0,
            Description: parts.Length > 2 ? parts[2] : ""
        ));
        var todos = todoLines;

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

    public void WriteDailyEntry(string markdown)
    {
        File.WriteAllText(_options.TodayFile, markdown);
    }
}
