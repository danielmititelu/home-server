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
        var lines = logs
            .Select(ToCsvLine)
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

        var lines = File.ReadLines(_options.LogFile);
        return ParseWorkoutLogs(lines);
    }

    public void WriteWorkoutReport(IEnumerable<string> markdownLines)
    {
        File.WriteAllLines(_options.ReportFile, markdownLines);
    }

    public static string ToCsvLine(WorkoutLog log)
    {
        return $"{log.Month},{log.Day},{log.Type},{log.Reps}";
    }

    public static IEnumerable<WorkoutLog> ParseWorkoutLogs(IEnumerable<string> csvLines)
    {
        return csvLines
            .Skip(1)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var parts = line.Split(',');
                return new WorkoutLog(
                    Month: parts[0],
                    Day: parts[1],
                    Type: parts[2],
                    Reps: parts[3]
                );
            });
    }
}
