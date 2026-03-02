namespace Vaultling.Services;

public class DailyEntryService(
    DailyEntryRepository dailyEntryRepository,
    WorkoutRepository workoutRepository,
    ExpenseRepository expenseRepository,
    TimeProvider timeProvider)
{
    public void ProcessDailyEntry()
    {
        var todayDate = timeProvider.GetLocalNow().ToIsoDateString();
        var yesterdayEntry = dailyEntryRepository.ReadDailyEntry();

        if (yesterdayEntry.Date.ToIsoDateString() == todayDate)
        {
            return;
        }

        workoutRepository.AppendWorkout(yesterdayEntry.ToWorkoutLogs());
        expenseRepository.AppendExpenses(yesterdayEntry.ToExpenseLogs());

        dailyEntryRepository.ArchiveDailyFile(yesterdayEntry.Date);

        var todayWorkouts = workoutRepository.GetTodayWorkout();
        var carryOverTodos = yesterdayEntry.Todos
            .Where(t => !t.Contains("[x]", StringComparison.OrdinalIgnoreCase));
        var newTodayEntry = new DailyEntry(
            Date: timeProvider.GetLocalNow(),
            Workouts: todayWorkouts,
            Todos: carryOverTodos,
            Expenses: []
        );
        dailyEntryRepository.WriteDailyEntry(newTodayEntry);
    }
}
