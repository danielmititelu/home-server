namespace Vaultling.Services.Repositories;

using Vaultling.Utils;

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
        if (parts.Length < 3)
        {
            Console.Error.WriteLine($"[WorkoutRepository] Schedule row for '{todayDayOfWeek}' has fewer than 3 columns.");
            return [];
        }
        return
        [
            new DailyWorkout(parts[1], ""),
            new DailyWorkout(parts[2], "")
        ];
    }

    public void AppendWorkout(IEnumerable<WorkoutLog> logs)
    {
        var lines = logs
            .Select(log => $"{log.Month},{log.Day},{log.Type},{log.Reps}")
            .ToList();

        if (lines.Count == 0)
        {
            return;
        }

        File.AppendAllLines(_options.LogFile, lines);
    }

    public IEnumerable<WorkoutLog> ReadWorkoutLogs()
    {
        if (!File.Exists(_options.LogFile))
        {
            return [];
        }

        return Utils.ParseCsv(File.ReadLines(_options.LogFile), parts => new WorkoutLog(
            Month: parts[0],
            Day: parts[1],
            Type: parts[2],
            Reps: parts[3]
        ));
    }

    public void WriteWorkoutReport(IEnumerable<string> markdownLines)
    {
        File.WriteAllLines(_options.ReportFile, markdownLines);
    }
}
