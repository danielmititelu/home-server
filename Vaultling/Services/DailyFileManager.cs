using Vaultling.Models;
using Vaultling.Services.Repositories;

namespace Vaultling.Services;

public class DailyFileManager(
    DailyFileRepository dailyFileRepository,
    WorkoutRepository workoutRepository,
    ExpenseRepository expenseRepository,
    TimeProvider timeProvider)
{
    public void Run()
    {
        var todayDate = timeProvider.GetUtcNow().ToString("yyyy-MM-dd");
        var yesterdayFile = dailyFileRepository.ReadDailyFile();

        if (yesterdayFile.Date.ToString("yyyy-MM-dd") == todayDate)
        {
            return;
        }

        workoutRepository.AppendWorkout(yesterdayFile.ToWorkoutLogs());
        expenseRepository.AppendExpenses(yesterdayFile.ToExpenseLogs());

        dailyFileRepository.ArchiveDailyFile(yesterdayFile.Date);

        var todayWorkouts = workoutRepository.GetTodayWorkout();
        var newTodayFile = new DailyFile(
            Date: timeProvider.GetUtcNow().DateTime,
            Workouts: todayWorkouts,
            Expenses: []
        );
        dailyFileRepository.WriteDailyFile(newTodayFile);
    }
}
