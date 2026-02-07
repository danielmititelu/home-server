using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vaultling.Models;
using Vaultling.Services;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

var services = new ServiceCollection();
services.Configure<FilePaths>(configuration.GetSection("FilePaths"));
services.AddSingleton(TimeProvider.System);
services.AddSingleton<VaultRepository>();
services.AddTransient<WorkoutService>();
services.AddTransient<DailyFileManager>();
services.AddTransient<ExpenseReportService>();

var provider = services.BuildServiceProvider();

// Run daily file management
provider.GetRequiredService<DailyFileManager>().Run();

// Generate reports
provider.GetRequiredService<ExpenseReportService>().Generate();
