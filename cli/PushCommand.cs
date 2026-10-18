using CommandLine;
using FishBucket;
using FishBucket.ApiClient;
using FishBucket.SyncClient;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using System.Text.Json;

namespace FishSyncClient.Cli;

[Verb("push", HelpText = "Mirror local directory to server bucket; may overwrite and delete server files immediately.")]
public class PushCommand : SyncCommandBase
{
    private readonly static JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true
    };

    [Value(0, Required = false, MetaName = "bucket-id", HelpText = "Bucket id. Defaults to config BucketId.")]
    public string? BucketId { get; set; }

    protected override async ValueTask<int> RunAsync()
    {
        var settings = await GetSettings(BucketId);

        using var httpClient = CreatePushHttpClient();
        using var apiHttpClient = CreatePushHttpClient();
        var syncFiles = CliSyncFiles.EnumerateLocalFiles(settings.Root, new PathOptions());
        var syncCollection = new SyncFileCollection(syncFiles);
        var progressAggregator = new ConcurrentByteProgressAggregator();
        var actionProgress = new SyncProgress<SyncActionProgress>(e =>
        {
            Console.WriteLine($"{e.EventType}: {e.Action.Path}");
        });
        var byteProgress = new SyncProgress<SyncActionByteProgress>(e =>
        {
            progressAggregator.Report(e.Progress);
        });

        var handler = new SimpleBucketSyncActionCollectionHandler(6, actionProgress, byteProgress);
        handler.Add(new HttpBucketSyncActionHandler(httpClient));
        var apiClient = CreateApiClient(settings, apiHttpClient);
        var syncTask = apiClient.Sync(settings.BucketId, syncCollection, handler);

        while (!syncTask.IsCompleted)
        {
            await Task.WhenAny(Task.Delay(100), syncTask);
            var progress = progressAggregator.AggregateProgress();
            Console.WriteLine($"{progress.GetRatio():p} ( {progress.ProgressedBytes:#,##} / {progress.TotalBytes:#,##} )");
        }

        var result = await syncTask;
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

        foreach (var action in result.RequiredActions)
        {
            Console.WriteLine($"{action.Path}: {action.Action.Type}");
            Console.WriteLine(JsonSerializer.Serialize(action.Action.Parameters, _jsonOptions));
        }

        return result.IsSuccess ? 0 : 1;
    }

    private static HttpClient CreatePushHttpClient()
    {
        var handler = new PushAuthenticationHandler(new HttpClientHandler(), Console.Error);
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromHours(1)
        };
    }
}
