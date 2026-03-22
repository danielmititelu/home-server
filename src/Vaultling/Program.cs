using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var services = new ServiceCollection();
services.Configure<DailyEntryOptions>(configuration.GetSection("DailyEntry"));
services.Configure<WorkoutOptions>(configuration.GetSection("Workout"));
services.Configure<ExpenseOptions>(configuration.GetSection("Expense"));
services.Configure<ErrorOptions>(configuration.GetSection("Error"));
services.Configure<CalendarOptions>(configuration.GetSection("Calendar"));
services.AddSingleton(TimeProvider.System);
services.AddSingleton<DailyEntryRepository>();
services.AddSingleton<WorkoutRepository>();
services.AddSingleton<ExpenseRepository>();
services.AddSingleton<ErrorRepository>();
services.AddSingleton<CalendarRepository>();
services.AddTransient<DailyEntryService>();
services.AddTransient<WorkoutService>();
services.AddTransient<ExpenseService>();
services.AddTransient<CalendarService>();
services.AddTransient<VaultlingRunner>();

var provider = services.BuildServiceProvider();

provider.GetRequiredService<VaultlingRunner>().Run();
