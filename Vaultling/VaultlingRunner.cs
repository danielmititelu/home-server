namespace Vaultling;

using Vaultling.Services;
using Vaultling.Services.Repositories;

public class VaultlingRunner(
    DailyFileManager dailyFileManager,
    ExpenseReportService expenseReportService,
    ErrorRepository errorRepository)
{
    public void Run()
    {
        try
        {
            dailyFileManager.Run();
            expenseReportService.Generate();
        }
        catch (Exception ex)
        {
            errorRepository.WriteErrorLog(ex);
            throw;
        }
    }
}
