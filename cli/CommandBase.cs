using CommandLine;

namespace FishSyncClient.Cli;

public abstract class CommandBase
{
    [Option("root")]
    public string? Root { get; set; }

    [Option("server")]
    public string? Server { get; set; }

    [Value(0, Required = true)]
    public string? Id { get; set; }

    public int Run()
    {
        return RunAsync().GetAwaiter().GetResult();
    }

    protected abstract ValueTask<int> RunAsync();

    protected string? GetHost()
    {
        if (!string.IsNullOrEmpty(Server))
            return Server;
        var env = Environment.GetEnvironmentVariable("FISH_SERVER");
        if (!string.IsNullOrEmpty(env))
            return env;
        return null;
    }
}