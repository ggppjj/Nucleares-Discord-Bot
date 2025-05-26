using Discord;
using Discord.Net;
using Discord.WebSocket;
using LibNuclearesWeb.NuclearesWeb;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

CancellationTokenSource? shutdown = new();
DiscordSocketConfig? config = new() { LogLevel = LogSeverity.Info };
DiscordSocketClient? client = new(config);

var appConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appConfig.json", optional: true, reloadOnChange: true)
    .Build();

var configIp = appConfig["IpAddress"];
var configPort = appConfig["Port"];

var gameIp = configIp;
int? gamePort = null;
if (int.TryParse(configPort, out var configPortParsed))
    gamePort = configPortParsed;

var ipArg = args.FirstOrDefault(e => e.StartsWith("/IpAddress="));
if (ipArg is not null)
    gameIp = ipArg["/IpAddress=".Length..];

var argPort = args.FirstOrDefault(e => e.StartsWith("/Port="));
if (argPort is not null && int.TryParse(argPort["/Port=".Length..], out var argPortParsed))
    gamePort = argPortParsed;

NuclearesWeb? nuclearesWebController = new(gameIp, gamePort);

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    shutdown.Cancel();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Cancel();

client.Ready += () => OnClientReady(client);
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

var tokenConfig = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("token.json", optional: false, reloadOnChange: true)
    .Build();

var token = tokenConfig["Token"];
if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("Missing bot token in token.json");
    return;
}

await client.LoginAsync(TokenType.Bot, token);
await client.StartAsync();

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
return;

async Task OnClientReady(DiscordSocketClient readyClient)
{
    List<ApplicationCommandProperties> applicationCommandProperties =
    [
        new SlashCommandBuilder()
            .WithName("execute-command")
            .WithDescription("Execute a command on the running user's game!")
            .AddOption(
                "command",
                ApplicationCommandOptionType.String,
                "Command to run.",
                isRequired: true
            )
            .Build(),
    ];

    try
    {
        _ = await readyClient.BulkOverwriteGlobalApplicationCommandsAsync(
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
