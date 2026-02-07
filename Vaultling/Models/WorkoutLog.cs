namespace Vaultling.Models;

public record WorkoutLog(string Month, string Day, string Type, string Reps)
{
    public string ToCsvLine()
    {
        return $"{Month},{Day},{Type},{Reps}";
    }

    public static IEnumerable<WorkoutLog> Parse(IEnumerable<string> csvLines)
    {
        return csvLines
            .Skip(1) // Skip header
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
