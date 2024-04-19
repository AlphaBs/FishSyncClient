using System.Text.Json;
using CommandLine;
using FishSyncClient.Common;
using FishSyncClient.Progress;
using FishSyncClient.Push;

namespace FishSyncClient.Cli;

[Verb("push")]
public class PushCommand : CommandBase
{
    [Value(0, Required = true)]
    public string? Id { get; set; }

    private readonly static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
          WriteIndented = true
    };

    protected override async ValueTask<int> RunAsync()
    {
        if (string.IsNullOrEmpty(Id))
            throw new ArgumentException("Id");
        if (string.IsNullOrEmpty(Root))
            Root = Environment.CurrentDirectory;
        
        var host = getHost();
        if (string.IsNullOrEmpty(host))
            throw new ArgumentException("host");

        var httpClient = new HttpClient();
        var pushClient = new PushClient(new Uri(host), httpClient);
        var files = RootedPath.FromDirectory(Root, new PathOptions())
            .Select(pushClient.CreateSyncFile);
        foreach (var file in files)
        {
            Console.WriteLine("Source: " + file.Path);
        }

        var fileProgress = new SyncProgress<FishFileProgressEventArgs>(e => 
            Console.WriteLine($"{e.EventType} {e.ProgressedFiles} / {e.TotalFiles} {e.CurrentFileName}"));
        var byteProgress = new SyncProgress<ByteProgress>(e => 
            Console.WriteLine(e.GetPercentage() + "%"));

        var handler = new SimpleBucketSyncActionCollectionHandler(Root, fileProgress, byteProgress);
        handler.Add(new HttpBucketSyncActionHandler(httpClient));
        var result = await pushClient.Push(Id, files, handler, default);

        if (result.IsSuccess)
        {
            Console.WriteLine("Success! updated at " + result.UpdatedAt);
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine("Fail!");
            Console.WriteLine();
        }

        foreach (var action in result.Actions)
        {
            Console.WriteLine($"{action.Path}: {action.Action.Type}");
            Console.WriteLine(JsonSerializer.Serialize(action.Action.Parameters, _jsonOptions));
        }

        return 0;
    }

    private string? getHost()
    {
        if (!string.IsNullOrEmpty(Server))
            return Server;
        var env = Environment.GetEnvironmentVariable("FISH_SERVER");
        if (!string.IsNullOrEmpty(env))
            return env;
        return null;
    }
}