namespace Vaultling.Services;

public class VaultlingRunner(
    DailyEntryService dailyEntryService,
    WorkoutService workoutService,
    ExpenseService expenseService)
{
    public void Run()
    {
        try
        {
            Console.WriteLine("Vaultling started");
            dailyEntryService.ProcessDailyEntry();
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
