namespace Vaultling.Services;

public class DailyEntryService(
    DailyEntryRepository dailyEntryRepository,
    WorkoutRepository workoutRepository,
    ExpenseRepository expenseRepository,
    CalendarRepository calendarRepository,
    TimeProvider timeProvider)
{
    public void ProcessDailyEntry()
    {
        var todayDate = timeProvider.GetLocalNow().ToIsoDateString();
        var yesterdayEntry = dailyEntryRepository.ReadDailyEntry();

        if (yesterdayEntry.Date.ToIsoDateString() == todayDate)
        {
            return;
        }

        workoutRepository.AppendWorkout(ToWorkoutLogs(yesterdayEntry));
        expenseRepository.AppendExpenses(ToExpenseLogs(yesterdayEntry));

        dailyEntryRepository.ArchiveDailyFile(yesterdayEntry.Date);

        var todayWorkouts = workoutRepository.GetTodayWorkout();
        var carryOverTodos = yesterdayEntry.Todos
            .Where(t => !t.Contains("[x]", StringComparison.OrdinalIgnoreCase));
        
        var currentYear = timeProvider.GetLocalNow().Year;
        var calendarEvents = calendarRepository.ReadCalendarOccurrences(currentYear);
        
        var newTodayEntry = new DailyEntry(
            Date: timeProvider.GetLocalNow(),
            Workouts: todayWorkouts,
            Todos: carryOverTodos,
            Expenses: [],
            CalendarEvents: calendarEvents
        );
        dailyEntryRepository.WriteDailyEntry(GenerateMarkdownForDailyEntry(newTodayEntry));
    }

    public static IEnumerable<WorkoutLog> ToWorkoutLogs(DailyEntry dailyEntry)
    {
        return dailyEntry.Workouts
            .Where(w => !string.IsNullOrWhiteSpace(w.Reps))
            .Select(w => new WorkoutLog(
                Month: dailyEntry.Date.Month.ToString("00"),
                Day: dailyEntry.Date.Day.ToString("00"),
                Type: w.Exercise,
                Reps: w.Reps
            ));
    }

    public static IEnumerable<ExpenseLog> ToExpenseLogs(DailyEntry dailyEntry)
    {
        return dailyEntry.Expenses
            .Where(e => e.Amount > 0)
            .Select(e => new ExpenseLog(
                Month: dailyEntry.Date.Month,
                Day: dailyEntry.Date.Day,
                Category: e.Category,
                Amount: e.Amount,
                Description: e.Description
            ));
    }

    public static IEnumerable<string> GenerateMarkdownForDailyEntry(DailyEntry dailyEntry)
    {
        var workoutLines = string.Join("\n", dailyEntry.Workouts.Select(w => $"{w.Exercise},{w.Reps}"));
        var todoLines = string.Join("\n", dailyEntry.Todos.Select(t => t));
        var upcomingEvents = dailyEntry.CalendarEvents
            .Where(e => e.Date > dailyEntry.Date.DateTime && e.Date <= dailyEntry.Date.DateTime.AddDays(14))
            .OrderBy(e => e.Date);
        var calendarLines = upcomingEvents.Any()
            ? string.Join("\n", upcomingEvents.Select(e =>
            {
                var timeStr = e.Date.TimeOfDay == TimeSpan.Zero
                    ? ""
                    : $" at {e.Date:HH:mm}";
                return $"- {e.Date:ddd MMM d}{timeStr}: {e.Note}";
            }))
            : "";

        var markdown = $"""
            # {DailySectionName.Date}
            {dailyEntry.Date.ToIsoDateString()}

            # {DailySectionName.Workout}
            exercise,reps
            {workoutLines}

            # {DailySectionName.Expenses}
            category,amount,description

            # {DailySectionName.Todo}
            {todoLines}

            # {DailySectionName.Calendar}
            {calendarLines}
            """;

        return markdown.Split('\n');
    }
}
