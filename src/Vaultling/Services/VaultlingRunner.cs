namespace Vaultling.Services;

public class VaultlingRunner(
    DailyEntryService dailyEntryService,
    WorkoutService workoutService,
    ExpenseService expenseService,
    CalendarService calendarService)
{
    public async Task RunAsync()
    {
        try
        {
            Console.WriteLine("Vaultling started");
            await dailyEntryService.ProcessDailyEntryAsync();
            workoutService.ProduceWorkoutReport();
            expenseService.ProduceExpenseReport();
            calendarService.ProduceCalendarReport();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled exception: {ex}");
            throw;
        }
    }
}
