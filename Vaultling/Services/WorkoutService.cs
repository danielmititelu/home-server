namespace Vaultling.Services;
using Vaultling.Models;

public class WorkoutService(
    VaultRepository vaultRepository,
    TimeProvider timeProvider)
{
    public void ProcessYesterdayWorkout(DailyFile yesterdayFile)
    {
        var completedExercises = yesterdayFile.Workout.Workouts
            .Where(e => !string.IsNullOrWhiteSpace(e.Reps))
            .ToList();

        if (completedExercises.Count == 0)
        {
            return;
        }

        var date = yesterdayFile.Date.Date;
        var logEntries = completedExercises.Select(e => new WorkoutLog(
            Month: date.Month.ToString("00"),
            Day: date.Day.ToString("00"),
            Type: e.Exercise,
            Reps: e.Reps
        )).ToList();

        vaultRepository.AppendToWorkoutCsv(logEntries);
    }

    public List<string> GetTodayWorkoutSection()
    {
        var schedules = vaultRepository.ReadWorkoutSchedules();
        var todayDayOfWeek = timeProvider.GetUtcNow().ToString("dddd");
        var todaySchedule = schedules.FirstOrDefault(s => s.DayOfWeek == todayDayOfWeek);

        if (todaySchedule == null)
        {
            return [];
        }

        return
        [
            $"# {DailySectionName.Workout}",
            "exercise,reps",
            $"{todaySchedule.FirstExercise},",
            $"{todaySchedule.SecondExercise},"
        ];
    }
}
