using CommandLine;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Server;
using FishSyncClient.Server.BucketSyncActions;
using System.Text.Json;

namespace FishSyncClient.Cli;

[Verb("push")]
public class PushCommand : CommandBase
{
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
        
        var host = GetHost();
        if (string.IsNullOrEmpty(host))
            throw new ArgumentException("host");

        var httpClient = new HttpClient();
        var syncFiles = RootedPath.FromDirectory(Root, new PathOptions()).Select(createLocalSyncFile);
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
        var apiClient = new FishApiClient(host, httpClient);
        var syncTask = apiClient.Sync(Id, syncCollection, handler);

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

        foreach (var action in result.Actions)
        {
            Console.WriteLine($"{action.Path}: {action.Action.Type}");
            Console.WriteLine(JsonSerializer.Serialize(action.Action.Parameters, _jsonOptions));
        }

        return 0;
    }

    private SyncFile createLocalSyncFile(RootedPath path)
    {
        var fileinfo = new FileInfo(path.GetFullPath());
        using var fs = File.OpenRead(fileinfo.FullName);
        var checksum = ChecksumAlgorithms.ComputeMD5(fs);
        return new LocalSyncFile(path)
        {
            Metadata = new SyncFileMetadata()
            {
                Size = fileinfo.Length,
                Checksum = checksum,
                ChecksumAlgorithm = "md5"
            }
        };
    }
}