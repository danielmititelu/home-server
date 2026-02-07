namespace Vaultling;

using Vaultling.Services;

public class VaultlingRunner(
    DailyFileManager dailyFileManager,
    ExpenseReportService expenseReportService)
{
    public void Run()
    {
        dailyFileManager.Run();
        expenseReportService.Generate();
    }
}
