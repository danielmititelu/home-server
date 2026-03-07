namespace Vaultling.Services;

public class VaultlingRunner(
    DailyEntryService dailyEntryService,
    WorkoutService workoutService,
    ExpenseService expenseService,
    ErrorRepository errorRepository)
{
    public void Run()
    {
        try
        {
            dailyEntryService.ProcessDailyEntry();
            workoutService.ProduceWorkoutReport();
            expenseService.ProduceExpenseReport();
        }
        catch (Exception ex)
        {
            errorRepository.WriteErrorLog(ex);
            throw;
        }
    }
}
