namespace Vaultling.Models;

public record DailyWorkout(string Exercise, string Reps);
public record DailyExpense(string Category, decimal Amount, string Description);
public enum DailySectionName
{
    Date,
    Workout,
    Expenses
}

public record DailyFile(DateTime Date, IEnumerable<DailyWorkout> Workouts, IEnumerable<DailyExpense> Expenses)
{
    public IEnumerable<WorkoutLog> ToWorkoutLogs()
    {
        return Workouts
            .Where(w => !string.IsNullOrWhiteSpace(w.Reps))
            .Select(w => new WorkoutLog(
                Month: Date.Month.ToString("00"),
                Day: Date.Day.ToString("00"),
                Type: w.Exercise,
                Reps: w.Reps
            ));
    }

    public IEnumerable<ExpenseLog> ToExpenseLogs()
    {
        return Expenses
            .Where(e => e.Amount > 0)
            .Select(e => new ExpenseLog(
                Month: Date.Month,
                Day: Date.Day,
                Category: e.Category,
                Amount: e.Amount,
                Description: e.Description
            ));
    }

    public IEnumerable<string> ToMarkdownLines()
    {
        var workoutLines = string.Join("\n", Workouts.Select(w => $"{w.Exercise},{w.Reps}"));
        
        var markdown = $"""
            # {DailySectionName.Date}
            {Date.ToIsoDateString()}

            # {DailySectionName.Workout}
            exercise,reps
            {workoutLines}

            # {DailySectionName.Expenses}
            category,amount,description
            """;

        return markdown.Split('\n');
    }

    public static DailyFile Parse(IEnumerable<string> lines)
    {
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

        var date = DateTime.Parse(sectionsContent[DailySectionName.Date.ToString()].First());
        var workouts = ParseDailyWorkouts(sectionsContent[DailySectionName.Workout.ToString()]);
        var expenses = ParseDailyExpenses(sectionsContent[DailySectionName.Expenses.ToString()]);

        return new DailyFile(date, workouts, expenses);
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
