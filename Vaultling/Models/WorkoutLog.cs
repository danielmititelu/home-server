namespace Vaultling.Models;

public record WorkoutLog(string Month, string Day, string Type, string Reps)
{
    public string ToCsvLine()
    {
        return $"{Month},{Day},{Type},{Reps}";
    }
}
