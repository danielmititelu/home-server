using Discord;
using Discord.WebSocket;
using HomeBot;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
{
    GatewayIntents = GatewayIntents.Guilds
}));

builder.Services.AddHostedService<BotService>();

var host = builder.Build();
host.Run();
