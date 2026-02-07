namespace Vaultling.Services;

using Microsoft.Extensions.Options;
using Vaultling.Models;

public class VaultRepository(IOptions<FilePaths> filePaths)
{
    private readonly FilePaths _filePaths = filePaths.Value;

    // Expense handling
    public List<Expense> ReadExpenses()
    {
        return ReadCsvFile<Expense>(_filePaths.Expenses);
    }

    public void WriteExpenses(IEnumerable<Expense> expenses)
    {
        var lines = new[] { "month,category,amount" }
            .Concat(expenses.Select(e => $"{e.Month},{e.Category},{e.Amount:0.00}"));
        File.AppendAllLines(_filePaths.Expenses, lines);
    }

    public void WriteExpensesReport(IEnumerable<string> lines)
    {
        File.WriteAllLines(_filePaths.ExpensesReport, lines);
    }

    // Workout handling
    public List<WorkoutSchedule> ReadWorkoutSchedules()
    {
        return ReadCsvFile<WorkoutSchedule>(_filePaths.WorkoutSchedule);
    }

    public void AppendToWorkoutCsv(IEnumerable<WorkoutLog> logs)
    {
        var lines = logs.Select(l => $"{l.Month},{l.Day},{l.Type},{l.Reps}");
        File.AppendAllLines(_filePaths.Workout, lines);
    }

    // Daily file handling
    public DailyFile ReadDailyFile()
    {
        var lines = File.ReadAllLines(_filePaths.Today);
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

        var dateSection = new DateSection(
            DateTime.Parse(sectionsContent[DailySectionName.Date.ToString()].First())
        );

        var workoutSection = new DailyWorkoutSection(
            ParseCsvTable<DailyWorkout>(sectionsContent[DailySectionName.Workout.ToString()])
        );

        return new DailyFile(dateSection, workoutSection);
    }

    public void ArchiveDailyFile()
    {
        var todayFilePath = _filePaths.Today;
        var archiveDir = _filePaths.DailyHistory;
        var archiveFilePath = Path.Combine(archiveDir, $"daily_{DateTime.UtcNow:yyyyMMdd}.md");

        if (!Directory.Exists(archiveDir))
        {
            Directory.CreateDirectory(archiveDir);
        }

        File.Move(todayFilePath, archiveFilePath);
    }

    public void WriteTodayFile(IEnumerable<string> lines)
    {
        var todayFilePath = _filePaths.Today;
        File.WriteAllLines(todayFilePath, lines);
    }

    // Utility methods
    private static List<T> ReadCsvFile<T>(string filePath) where T : IParseable<T>
    {
        return ParseCsvTable<T>(File.ReadAllLines(filePath).ToList());
    }

    private static List<T> ParseCsvTable<T>(List<string> lines) where T : IParseable<T>
    {
        return lines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => T.Parse(line.Split(',')))
            .ToList();
    }
}
