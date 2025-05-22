using Discord;
using Discord.Net;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

_ = args;

var shutdown = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};

AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

var config = new DiscordSocketConfig { LogLevel = LogSeverity.Info };
var client = new DiscordSocketClient(config);
client.Ready += ClientReady;
client.SlashCommandExecuted += SlashCommandHandler;
client.Log += (logEntry) =>
{
    Console.WriteLine(
        $"[{DateTime.Now:HH:mm:ss}] [{logEntry.Severity}] [{logEntry.Source}] {logEntry.Message}"
    );
    if (logEntry.Exception is not null)
        Console.WriteLine(logEntry.Exception);
    return Task.CompletedTask;
};

var appConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("token.json", optional: false, reloadOnChange: true)
    .Build();

var token = appConfig["Token"];

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

async Task ClientReady()
{
    var testCommand = new SlashCommandBuilder()
        .WithName("test-command")
        .WithDescription("Testing, 123!")
        .Build();

    var globalCommands = await client.GetGlobalApplicationCommandsAsync();

    if (!globalCommands.Any(c => c.Name == "test-command"))
    {
        try
        {
            _ = await client.CreateGlobalApplicationCommandAsync(testCommand);
        }
        catch (HttpException exception)
        {
            Console.WriteLine(JsonConvert.SerializeObject(exception.Errors, Formatting.Indented));
        }
    }
}

async Task SlashCommandHandler(SocketSlashCommand command) =>
    await command.RespondAsync("so much boilerplate for a single slash command sheesh");

try
{
    await Task.Delay(Timeout.Infinite, shutdown.Token);
}
catch (TaskCanceledException)
{
    await client.LogoutAsync();
    await client.StopAsync();
    client.Dispose();
}
