using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vaultling.Utils;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .Build();

var currentYear = TimeProvider.System.GetLocalNow().Year;

var services = new ServiceCollection();
services.Configure<DailyEntryOptions>(configuration.GetSection("DailyEntry"));
services.Configure<WorkoutOptions>(configuration.GetSection("Workout"));
services.Configure<ExpenseOptions>(configuration.GetSection("Expense"));
services.Configure<CalendarOptions>(configuration.GetSection("Calendar"));
services.PostConfigure<WorkoutOptions>(opts =>
{
    opts.CurrentYearLogFile = Utils.ResolveYearPath(opts.LogFileTemplate, currentYear);
    opts.CurrentYearReportFile = Utils.ResolveYearPath(opts.ReportFileTemplate, currentYear);
});
services.PostConfigure<ExpenseOptions>(opts =>
{
    opts.PreviousYearDataFile = Utils.ResolveYearPath(opts.DataFileTemplate, currentYear - 1);
    opts.CurrentYearDataFile = Utils.ResolveYearPath(opts.DataFileTemplate, currentYear);
    opts.CurrentYearReportFile = Utils.ResolveYearPath(opts.ReportFileTemplate, currentYear);
});
services.AddSingleton(TimeProvider.System);
services.AddSingleton<DailyEntryRepository>();
services.AddSingleton<WorkoutRepository>();
services.AddSingleton<ExpenseRepository>();
services.AddSingleton<CalendarRepository>();
services.AddHttpClient<WeatherRepository>();
services.AddTransient<DailyEntryService>();
services.AddTransient<WorkoutService>();
services.AddTransient<ExpenseService>();
services.AddTransient<CalendarService>();
services.AddTransient<VaultlingRunner>();

var provider = services.BuildServiceProvider();

await provider.GetRequiredService<VaultlingRunner>().RunAsync();
