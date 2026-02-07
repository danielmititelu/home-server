using Vaultling.Models;

namespace Vaultling.Services;

public class DailyFileManager(
    VaultRepository vaultRepository,
    WorkoutService workoutHandler,
    TimeProvider timeProvider)
{
    public void Run()
    {
        var todayDate = timeProvider.GetUtcNow().ToString("yyyy-MM-dd");
        var yesterdayFile = vaultRepository.ReadDailyFile();
        Console.WriteLine($"Today's file date: {yesterdayFile.Date.Date:yyyy-MM-dd}, Today's date: {todayDate}");
        if (yesterdayFile.Date.Date.ToString("yyyy-MM-dd") == todayDate)
        {
            return;
        }

        workoutHandler.ProcessYesterdayWorkout(yesterdayFile);
        vaultRepository.ArchiveDailyFile();
        vaultRepository.WriteTodayFile(GetTodayFileContent());
    }

    private List<string> GetTodayFileContent()
    {
        var todayWorkoutSection = workoutHandler.GetTodayWorkoutSection();
        var todayContent = new List<string>
        {
            $"# {DailySectionName.Date}",
            timeProvider.GetUtcNow().ToString("yyyy-MM-dd"),
            "",
        };
        todayContent.AddRange(todayWorkoutSection);
        return todayContent;
    }
}
