using CommandLine;

namespace FishSyncClient.Cli;

[Verb("directory")]
internal class DirectoryCommand : CommandBase
{
    protected override ValueTask<int> RunAsync()
    {
        if (string.IsNullOrEmpty(Root))
            throw new InvalidOperationException("empty Root");

        var paths = RootedPath.FromDirectory(Root, new PathOptions());
        foreach (var path in paths)
        {
            Console.WriteLine($"{path.SubPath} ({path.GetFullPath()})");
        }

        return new ValueTask<int>(0);
    }
}
