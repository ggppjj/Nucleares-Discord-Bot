using Discord;
using Discord.Net;
using Discord.WebSocket;
using LibNuclearesWeb.NuclearesWeb;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

_ = args;

var shutdown = new CancellationTokenSource();
var nuclearesWebController = new NuclearesWeb();

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
    List<ApplicationCommandProperties> applicationCommandProperties = [];

    applicationCommandProperties.Add(
        new SlashCommandBuilder()
            .WithName("execute-command")
            .WithDescription("Execute a command on the running user's game!")
            .AddOption(
                "command",
                ApplicationCommandOptionType.String,
                "Command to run.",
                isRequired: true
            )
            .Build()
    );

    try
    {
        _ = await client.BulkOverwriteGlobalApplicationCommandsAsync(
            [.. applicationCommandProperties]
        );
    }
    catch (HttpException exception)
    {
        Console.WriteLine(JsonConvert.SerializeObject(exception.Errors, Formatting.Indented));
    }
}

async Task SlashCommandHandler(SocketSlashCommand command)
{
    switch (command.CommandName)
    {
        case "execute-command":
            var response = nuclearesWebController.GetDataFromGameAsync(
                command.Data.Options.First().Value.ToString()!
            );
            await command.RespondAsync(await response);
            break;
        default:
            await command.RespondAsync("Command unknown, somehow!");
            break;
    }
}

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
