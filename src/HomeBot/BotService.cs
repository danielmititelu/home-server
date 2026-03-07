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
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "/usr/local/bin/metis",
            Arguments = "status",
            RedirectStandardOutput = true,
            UseShellExecute = false
        })!;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
