using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var services = new ServiceCollection();
services.Configure<DailyFileOptions>(configuration.GetSection("DailyFile"));
services.Configure<WorkoutOptions>(configuration.GetSection("Workout"));
services.Configure<ExpenseOptions>(configuration.GetSection("Expense"));
services.Configure<ErrorOptions>(configuration.GetSection("Error"));
services.AddSingleton(TimeProvider.System);
services.AddSingleton<DailyFileRepository>();
services.AddSingleton<WorkoutRepository>();
services.AddSingleton<ExpenseRepository>();
services.AddSingleton<ErrorRepository>();
services.AddTransient<DailyFileService>();
services.AddTransient<ExpenseService>();
services.AddTransient<VaultlingRunner>();

var provider = services.BuildServiceProvider();

provider.GetRequiredService<VaultlingRunner>().Run();
