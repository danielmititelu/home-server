namespace Vaultling.Services;

public class VaultlingRunner(
    DailyEntryService dailyEntryService,
    WorkoutService workoutService,
    ExpenseService expenseService)
{
    public async Task RunAsync()
    {
        try
        {
            Console.WriteLine("Vaultling started");
            await dailyEntryService.ProcessDailyEntryAsync();
            workoutService.ProduceWorkoutReport();
            expenseService.ProduceExpenseReport();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Unhandled exception: {ex}");
            throw;
        }
    }
}
