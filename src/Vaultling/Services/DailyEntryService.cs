namespace Vaultling.Services;
using Utils;

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
        var todoItems = dailyEntry.Todos.ToList();
        var todoLines = todoItems.Count > 0
            ? string.Join("\n", todoItems)
            : "- [ ]";
        var today = dailyEntry.Date.Date;
        var calendarReportLink = Utils.GetCalendarReportMonthLink(dailyEntry.Date.DateTime);
        var upcomingEvents = dailyEntry.CalendarEvents
            .Where(e => e.Date > dailyEntry.Date.DateTime && e.Date <= dailyEntry.Date.DateTime.AddDays(14))
            .OrderBy(e => e.Date);
        var calendarLines = upcomingEvents.Any()
            ? string.Join("\n", upcomingEvents.Select(e =>
            {
                var dateTimeLabel = Utils.GetRelativeDateTimeLabel(e.Date, today);
                return $"- {dateTimeLabel}: {e.Note}";
            }))
            : "";

        var markdown = $"""
            # {DailySectionName.Date}
            {dailyEntry.Date.ToIsoDateString()}

            # {DailySectionName.Calendar}
            {calendarLines}
            - {calendarReportLink}

            # {DailySectionName.Workout}
            exercise,reps
            {workoutLines}

            # {DailySectionName.Expenses}
            category,amount,description

            # {DailySectionName.Todo}
            {todoLines}
            """;

        return markdown.Split('\n');
    }
}
