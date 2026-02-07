namespace Vaultling.Models;

public record Expense(int Month, int Day, string Category, decimal Amount, string Description) : IParseable<Expense>
{
    public static Expense Parse(string[] parts)
    {
        return new Expense(
            Month: int.Parse(parts[0]),
            Day: int.Parse(parts[1]),
            Category: parts[2],
            Amount: decimal.Parse(parts[3]),
            Description: parts[4]
        );
    }
}

public record WorkoutSchedule(string DayOfWeek, string FirstExercise, string SecondExercise) : IParseable<WorkoutSchedule>
{
    public static WorkoutSchedule Parse(string[] parts)
    {
        return new WorkoutSchedule(
            DayOfWeek: parts[0],
            FirstExercise: parts[1],
            SecondExercise: parts[2]
        );
    }
}

public record WorkoutLog(string Month, string Day, string Type, string Reps) : IParseable<WorkoutLog>
{
    public static WorkoutLog Parse(string[] parts)
    {
        return new WorkoutLog(
            Month: parts[0],
            Day: parts[1],
            Type: parts[2],
            Reps: parts[3]
        );
    }
}

public record TodayExercise(string Exercise, string Reps) : IParseable<TodayExercise>
{
    public static TodayExercise Parse(string[] parts)
    {
        return new TodayExercise(
            Exercise: parts[0],
            Reps: parts.Length > 1 ? parts[1] : ""
        );
    }
}

public record DailyFile(DateSection Date, DailyWorkoutSection Workout);
public record DateSection(DateTime Date) : IDailySection
{
    public DailySectionName Name => DailySectionName.Date;
    
    public static DateSection Parse(string[] lines)
    {
        if (lines.Length > 0 && DateTime.TryParse(lines[0], out var date))
        {
            return new DateSection(date);
        }
        throw new FormatException($"Could not parse date from daily file section");
    }
}

public record DailyWorkoutSection(List<DailyWorkout> Workouts) : IDailySection
{
    public DailySectionName Name => DailySectionName.Workout;
}

public record DailyWorkout(string Exercise, string Reps) : IParseable<DailyWorkout>
{
    public static DailyWorkout Parse(string[] parts)
    {
        return new DailyWorkout(
            Exercise: parts[0],
            Reps: parts.Length > 1 ? parts[1] : ""
        );
    }
}

public record FilePaths
{
    public string Expenses { get; set; } = "";
    public string ExpensesReport { get; set; } = "";
    public string Workout { get; set; } = "";
    public string WorkoutSchedule { get; set; } = "";
    public string Today { get; set; } = "";
    public string DailyHistory { get; set; } = "";
}

public interface IParseable<T> where T : IParseable<T>
{
    static abstract T Parse(string[] parts);
}

public interface IDailySection
{
    DailySectionName Name { get; }
}

public enum DailySectionName
{
    Date,
    Workout
}

public static class DailySectionNameExtensions
{
    public static string ToSectionHeader(this DailySectionName section)
    {
        return section.ToString();
    }

    public static DailySectionName? FromString(string sectionName)
    {
        return Enum.TryParse<DailySectionName>(sectionName, out var result) ? result : null;
    }
}