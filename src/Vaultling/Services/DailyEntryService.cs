namespace Vaultling.Services;
using Utils;

public class DailyEntryService(
    DailyEntryRepository dailyEntryRepository,
    WorkoutRepository workoutRepository,
    ExpenseRepository expenseRepository,
    CalendarRepository calendarRepository,
    WeatherRepository weatherRepository,
    TimeProvider timeProvider)
{
    public async Task ProcessDailyEntryAsync()
    {
        var todayDate = timeProvider.GetLocalNow().ToIsoDateString();
        var yesterdayEntry = dailyEntryRepository.ReadDailyEntry();

        if (yesterdayEntry.Date.ToIsoDateString() == todayDate)
        {
            Console.WriteLine("Today's entry already exists. Skipping daily entry processing.");
            return;
        }

        workoutRepository.AppendWorkout(yesterdayEntry.Workouts
            .Where(w => !string.IsNullOrWhiteSpace(w.Reps))
            .Select(w => new WorkoutLog(
                Month: yesterdayEntry.Date.Month.ToString("00"),
                Day: yesterdayEntry.Date.Day.ToString("00"),
                Type: w.Exercise,
                Reps: w.Reps
            )));
        expenseRepository.AppendExpenses(yesterdayEntry.Expenses
            .Where(e => e.Amount > 0)
            .Select(e => new ExpenseLog(
                Month: yesterdayEntry.Date.Month,
                Day: yesterdayEntry.Date.Day,
                Category: e.Category,
                Amount: e.Amount,
                Description: e.Description
            )));

        dailyEntryRepository.ArchiveDailyFile(yesterdayEntry.Date);

        var todayWorkouts = workoutRepository.GetTodayWorkout();
        var carryOverTodos = yesterdayEntry.Todos
            .Where(t => !t.Contains("[x]", StringComparison.OrdinalIgnoreCase));
        
        var currentYear = timeProvider.GetLocalNow().Year;
        var calendarEvents = calendarRepository.ReadCalendarOccurrences(currentYear);
        
        var city = yesterdayEntry.City;
        var travelCity = calendarRepository.GetTravelCityForDate(timeProvider.GetLocalNow().Date, currentYear);
        var weather = await weatherRepository.FetchWeatherAsync(travelCity ?? city);

        var newTodayEntry = new DailyEntry(
            Date: timeProvider.GetLocalNow(),
            Workouts: todayWorkouts,
            Todos: carryOverTodos,
            Expenses: [],
            CalendarEvents: calendarEvents,
            City: city
        );
        dailyEntryRepository.WriteDailyEntry(GenerateMarkdownForDailyEntry(newTodayEntry, weather));
    }

    public static IEnumerable<string> GenerateMarkdownForDailyEntry(DailyEntry dailyEntry, WeatherInfo? weather = null)
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
                var eventText = $"{dateTimeLabel}: {e.Note}";
                var renderedEventText = e.Cancelled ? $"~~{eventText}~~" : eventText;
                return renderedEventText;
            }))
            : "";

        var weatherLines = weather is { Summary: not null, Sunrise: not null, Sunset: not null }
            ? $"{weather.City}\n{weather.Summary}\n🌅 {weather.Sunrise} 🌇 {weather.Sunset}"
            : dailyEntry.City;

        var markdown = $"""
            # {DailySectionName.Date}
            {dailyEntry.Date.ToIsoDateString()}

            # {DailySectionName.Weather}
            {weatherLines}

            # {DailySectionName.Calendar}
            {calendarLines}
            {calendarReportLink}

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
