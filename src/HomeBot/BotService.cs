using System.Diagnostics;
using Discord;
using Discord.WebSocket;

namespace HomeBot;

public class BotService(
    DiscordSocketClient client,
    IConfiguration configuration,
    ILogger<BotService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = configuration["DISCORD_TOKEN"]
            ?? throw new InvalidOperationException("DISCORD_TOKEN is not set");

        client.Log += msg =>
        {
            logger.LogInformation("{Message}", msg.ToString());
            return Task.CompletedTask;
        };

        client.Ready += async () =>
        {
            var pingCommand = new SlashCommandBuilder()
                .WithName("ping")
                .WithDescription("Pong!");

            var statusCommand = new SlashCommandBuilder()
                .WithName("status")
                .WithDescription("Get Raspberry Pi system status");

            await client.BulkOverwriteGlobalApplicationCommandsAsync(
            [
                pingCommand.Build(),
                statusCommand.Build()
            ]);

            logger.LogInformation("Slash commands registered");
        };

        client.SlashCommandExecuted += HandleSlashCommand;

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await client.StopAsync();
        await base.StopAsync(cancellationToken);
    }

    private async Task HandleSlashCommand(SocketSlashCommand command)
    {
        switch (command.Data.Name)
        {
            case "ping":
                await command.RespondAsync("pong 🏓");
                break;
            case "status":
                await command.RespondAsync(GetStatus());
                break;
        }
    }

    private static string GetStatus()
    {
        var cpuUsage = GetCpuUsage();
        var memInfo = GetMemoryUsage();
        var temp = GetTemperature();

        return $"""
            🖥️ **System Status**
            • CPU Usage: `{cpuUsage:F1}%`
            • Memory Usage: `{memInfo:F1}%`
            • Temperature: `{temp:F1} °C`
            """;
    }

    private static double GetCpuUsage()
    {
        var startTime = DateTime.UtcNow;
        var startCpu = Process.GetCurrentProcess().TotalProcessorTime;
        Thread.Sleep(500);
        var endTime = DateTime.UtcNow;
        var endCpu = Process.GetCurrentProcess().TotalProcessorTime;

        var cpuUsed = (endCpu - startCpu).TotalMilliseconds;
        var elapsed = (endTime - startTime).TotalMilliseconds;
        return cpuUsed / (elapsed * Environment.ProcessorCount) * 100;
    }

    private static double GetMemoryUsage()
    {
        var lines = File.ReadAllLines("/proc/meminfo");
        long total = 0, available = 0;
        foreach (var line in lines)
        {
            if (line.StartsWith("MemTotal:"))
                total = ParseMemInfoLine(line);
            else if (line.StartsWith("MemAvailable:"))
                available = ParseMemInfoLine(line);
        }
        return total > 0 ? (total - available) / (double)total * 100 : 0;
    }

    private static long ParseMemInfoLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return long.TryParse(parts[1], out var value) ? value : 0;
    }

    private static double GetTemperature()
    {
        try
        {
            var raw = File.ReadAllText("/sys/class/thermal/thermal_zone0/temp").Trim();
            return int.TryParse(raw, out var millideg) ? millideg / 1000.0 : 0;
        }
        catch
        {
            return 0;
        }
    }
}
