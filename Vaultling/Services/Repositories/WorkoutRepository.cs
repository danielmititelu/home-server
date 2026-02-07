namespace Vaultling.Services.Repositories;

public class WorkoutRepository(IOptions<WorkoutOptions> options, TimeProvider timeProvider)
{
    private readonly WorkoutOptions _options = options.Value;

    public List<DailyWorkout> GetTodayWorkout()
    {
        var todayDayOfWeek = timeProvider.GetLocalNow().ToString("dddd");
        var todayLine = File.ReadLines(_options.ScheduleFile)
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .FirstOrDefault(line => line.StartsWith(todayDayOfWeek));

        if (todayLine == null)
        {
            return [];
        }

        var parts = todayLine.Split(',');
        return
        [
            new DailyWorkout(parts[1], ""),
            new DailyWorkout(parts[2], "")
        ];
    }

    public void AppendWorkout(IEnumerable<WorkoutLog> logs)
    {
        var lines = logs.Select(l => l.ToCsvLine()).ToList();
        if (lines.Count == 0)
        {
            return;
        }
        File.AppendAllLines(_options.LogFile, lines);
    }
}
