namespace Vaultling.Services;

public class DailyFileService(
    DailyFileRepository dailyFileRepository,
    WorkoutRepository workoutRepository,
    ExpenseRepository expenseRepository,
    TimeProvider timeProvider)
{
    public void ProcessDailyFile()
    {
        var todayDate = timeProvider.GetLocalNow().ToIsoDateString();
        var yesterdayFile = dailyFileRepository.ReadDailyFile();

        if (yesterdayFile.Date.ToIsoDateString() == todayDate)
        {
            return;
        }

        workoutRepository.AppendWorkout(yesterdayFile.ToWorkoutLogs());
        expenseRepository.AppendExpenses(yesterdayFile.ToExpenseLogs());

        dailyFileRepository.ArchiveDailyFile(yesterdayFile.Date);

        var todayWorkouts = workoutRepository.GetTodayWorkout();
        var newTodayFile = new DailyFile(
            Date: timeProvider.GetLocalNow(),
            Workouts: todayWorkouts,
            Expenses: []
        );
        dailyFileRepository.WriteDailyFile(newTodayFile);
    }
}
