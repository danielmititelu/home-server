namespace Vaultling.Services;

public class VaultlingRunner(
    DailyFileService dailyFileService,
    ExpenseService expenseService,
    ErrorRepository errorRepository)
{
    public void Run()
    {
        try
        {
            dailyFileService.ProcessDailyFile();
            expenseService.ProduceExpenseReport();
        }
        catch (Exception ex)
        {
            errorRepository.WriteErrorLog(ex);
            throw;
        }
    }
}
