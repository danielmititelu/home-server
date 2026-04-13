using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vaultling.Utils;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var currentYear = TimeProvider.System.GetLocalNow().Year;

var services = new ServiceCollection();
services.Configure<DailyEntryOptions>(configuration.GetSection("DailyEntry"));
services.Configure<WorkoutOptions>(configuration.GetSection("Workout"));
services.Configure<ExpenseOptions>(configuration.GetSection("Expense"));
services.Configure<CalendarOptions>(configuration.GetSection("Calendar"));
services.PostConfigure<WorkoutOptions>(opts =>
{
    opts.LogFile = Utils.ResolveYearPath(opts.LogFile, currentYear);
    opts.ReportFile = Utils.ResolveYearPath(opts.ReportFile, currentYear);
});
services.PostConfigure<ExpenseOptions>(opts =>
{
    opts.PreviousYearDataFile = Utils.ResolveYearPath(opts.CurrentYearDataFile, currentYear - 1);
    opts.CurrentYearDataFile = Utils.ResolveYearPath(opts.CurrentYearDataFile, currentYear);
    opts.ReportFile = Utils.ResolveYearPath(opts.ReportFile, currentYear);
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
