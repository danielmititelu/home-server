namespace Vaultling.Services;

public class VaultlingRunner(
    DailyEntryService dailyEntryService,
    ExpenseService expenseService,
    ErrorRepository errorRepository)
{
    public void Run()
    {
        try
        {
            dailyEntryService.ProcessDailyEntry();
            expenseService.ProduceExpenseReport();
        }
        catch (Exception ex)
        {
            errorRepository.WriteErrorLog(ex);
            throw;
        }
    }
}
