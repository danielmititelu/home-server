namespace Vaultling.Services;
using Utils;

public class DailyEntryService(
    DailyEntryRepository dailyEntryRepository,
    WorkoutRepository workoutRepository,
    ExpenseRepository expenseRepository,
    TimeProvider timeProvider)
{
    public void ProcessDailyEntry()
    {
        var now = timeProvider.GetLocalNow();
        var todayDate = now.ToIsoDateString();
        var yesterdayEntry = dailyEntryRepository.ReadDailyEntry();

        if (yesterdayEntry.Date.ToIsoDateString() == todayDate)
        {
            Console.WriteLine("Today's entry already exists. Skipping daily entry processing.");
            return;
        }

        var workoutLogs = yesterdayEntry.Workouts
            .Where(w => !string.IsNullOrWhiteSpace(w.Reps))
            .Select(w => new WorkoutLog(
                Month: yesterdayEntry.Date.Month,
                Day: yesterdayEntry.Date.Day,
                Type: w.Exercise,
                Reps: w.Reps.Replace(',', '-')
            ));

        var expenseLogs = yesterdayEntry.Expenses
            .Where(e => e.Amount > 0)
            .Select(e => new ExpenseLog(
                Year: yesterdayEntry.Date.Year,
                Month: yesterdayEntry.Date.Month,
                Day: yesterdayEntry.Date.Day,
                Category: e.Category,
                Amount: e.Amount,
                Description: e.Description
            ));

        var todayWorkouts = workoutRepository.GetTodayWorkout();
        var carryOverTodos = yesterdayEntry.Todos
            .Where(t => !t.Contains("[x]", StringComparison.OrdinalIgnoreCase));

        var newTodayEntry = new DailyEntry(
            Date: now,
            Workouts: todayWorkouts,
            Todos: carryOverTodos,
            Expenses: []
        );
        var newTodayMarkdown = GenerateMarkdownForDailyEntry(newTodayEntry);

        workoutRepository.AppendWorkout(workoutLogs);
        expenseRepository.AppendExpenses(expenseLogs);
        dailyEntryRepository.ArchiveDailyFile(yesterdayEntry.Date);
        dailyEntryRepository.WriteDailyEntry(newTodayMarkdown);
    }

    public static string GenerateMarkdownForDailyEntry(DailyEntry dailyEntry)
    {
        var workoutLines = string.Join("\n", dailyEntry.Workouts.Select(w => $"{w.Exercise},{w.Reps}"));
        var todoItems = dailyEntry.Todos.ToList();
        var todoLines = todoItems.Count > 0
            ? string.Join("\n", todoItems)
            : "- [ ]";

        var markdown = $"""
            # {DailySectionName.Date}
            {dailyEntry.Date.ToIsoDateString()}

            # {DailySectionName.Workout}
            exercise,reps
            {workoutLines}

            # {DailySectionName.Expenses}
            category,amount,description

            # {DailySectionName.Todo}
            {todoLines}
            """;

        return markdown;
    }
}
