using CommandLine;

namespace FishSyncClient.Cli;

[Verb("pull")]
public class PullCommand : CommandBase
{
    [Value(0, Required = true)]
    public string? Id { get; set; }

    protected override ValueTask<int> RunAsync()
    {
        throw new NotImplementedException();
    }
}