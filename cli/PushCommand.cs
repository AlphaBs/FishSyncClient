using FishBucket;
using FishBucket.ApiClient;
using FishBucket.SyncClient;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using System.Text.Json;

namespace FishSyncClient.Cli;

public class PushCommand : SyncCommandBase
{
    protected override async ValueTask<int> RunAsync()
    {
        var settings = await GetSettings(BucketId);
        EnsureAbsoluteRoot(settings);
        // 없는 폴더를 push하면 빈 목록으로 서버 파일이 모두 삭제되므로 막는다.
        if (!Directory.Exists(settings.Root))
        {
            throw new DirectoryNotFoundException($"Root directory does not exist: {settings.Root}");
        }

        using var httpClient = CreateHttpClient();
        using var apiHttpClient = CreateHttpClient();
        var paths = RootedPath.FromDirectory(settings.Root, new PathOptions())
            .ToArray();
        if (!ConfirmWarnings(paths.Select(path => path.SubPath), settings))
        {
            return 1;
        }
        var syncFiles = paths
            .Select(CliSyncFiles.CreateLocalFile)
            .ToArray();
        var syncCollection = new SyncFileCollection(syncFiles);
        var progressAggregator = new ConcurrentByteProgressAggregator();
        var actionProgress = new SyncProgress<SyncActionProgress>(e =>
        {
            Output.WriteLine($"{e.EventType}: {e.Action.Path}");
        });
        var byteProgress = new SyncProgress<SyncActionByteProgress>(e =>
        {
            progressAggregator.Report(e.Progress);
        });

        var handler = new SimpleBucketSyncActionCollectionHandler(6, actionProgress, byteProgress);
        handler.Add(new HttpBucketSyncActionHandler(httpClient));
        var apiClient = await CreateApiClient(settings, apiHttpClient);
        var syncTask = apiClient.Sync(settings.BucketId, syncCollection, handler);

        while (!syncTask.IsCompleted)
        {
            await Task.WhenAny(Task.Delay(100), syncTask);
            var progress = progressAggregator.AggregateProgress();
            Output.WriteLine($"{progress.GetRatio():p} ( {progress.ProgressedBytes:#,##} / {progress.TotalBytes:#,##} )");
        }

        var result = await syncTask;
        if (result.IsSuccess)
        {
            Output.WriteLine("Success! updated at " + result.UpdatedAt);
            Output.WriteLine();
        }
        else
        {
            Output.WriteLine("Fail!");
            Output.WriteLine();
        }

        foreach (var action in result.RequiredActions)
        {
            Output.WriteLine($"{action.Path}: {action.Action.Type}");
            Output.WriteLine(JsonSerializer.Serialize(action.Action.Parameters, CliJsonContext.Default.IReadOnlyDictionaryStringString));
        }

        return result.IsSuccess ? 0 : 1;
    }

    protected override HttpClient CreateHttpClient()
    {
        var handler = new PushAuthenticationHandler(new HttpClientHandler(), ErrorOutput);
        return new HttpClient(handler)
        {
            Timeout = TimeSpan.FromHours(1)
        };
    }
}
