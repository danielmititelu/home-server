namespace Vaultling.Configuration;

public class ExpenseOptions
{
    public string DataFileTemplate { get; set; } = "";
    public string CurrentYearDataFile { get; set; } = "";
    public string PreviousYearDataFile { get; set; } = "";
    public string ReportFileTemplate { get; set; } = "";
    public string CurrentYearReportFile { get; set; } = "";
}
