using CommandLine;
using FishBucket;
using FishBucket.ApiClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;

namespace FishSyncClient.Cli;

[Verb("pull", HelpText = "Download bucket files into the local root.")]
public class PullCommand : CommandBase
{
    [Value(0, Required = false, MetaName = "bucket-id", HelpText = "Bucket id. Defaults to config BucketId.")]
    public string? BucketId { get; set; }

    protected override async ValueTask<int> RunAsync()
    {
        var settings = await GetSettings(BucketId);

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromHours(1) };
        var pathOptions = new PathOptions();

        var apiClient = CreateApiClient(settings, httpClient);
        var bucketFiles = await apiClient.GetBucketFiles(settings.BucketId);
        var syncFiles = bucketFiles.Files.Select(file => CliSyncFiles.CreateHttpFile(file, httpClient, pathOptions)).ToArray();

        var progressAggregator = new ConcurrentByteProgressAggregator();
        var fileProgress = new SyncProgress<FileProgressEvent>(e =>
        {
            Console.WriteLine($"[{e.EventType}][{e.ProgressedFiles}/{e.TotalFiles}] {e.CurrentFileName}");
        });
        var byteProgress = new SyncProgress<SyncFileByteProgress>(e =>
        {
            progressAggregator.Report(e.Progress);
        });

        var comparerFactory = new LocalFileComparerFactory();
        var syncer = new LocalSyncer(
            settings.Root,
            pathOptions,
            new ParallelSyncFilePairSyncer());

        var syncTask = syncer.CompareAndSyncFiles(syncFiles, comparerFactory.CreateFullComparer(), new SyncerOptions
        {
            FileProgress = fileProgress,
            ByteProgress = byteProgress
        });

        while (!syncTask.IsCompleted)
        {
            await Task.WhenAny(syncTask, Task.Delay(100));
            var progress = progressAggregator.AggregateProgress();
            Console.WriteLine($"{progress.GetRatio():p} ( {progress.ProgressedBytes:#,##} / {progress.TotalBytes:#,##} )");
        }

        var syncResult = await syncTask;
        Console.WriteLine($"\nIdentical files ({syncResult.IdenticalFilePairs.Count}): ");
        foreach (var identical in syncResult.IdenticalFilePairs)
        {
            Console.WriteLine(identical.Source.Path.SubPath);
        }

        Console.WriteLine($"\nUpdated files ({syncResult.UpdatedFilePairs.Count}): ");
        foreach (var updated in syncResult.UpdatedFilePairs)
        {
            Console.WriteLine(updated.Source.Path.SubPath);
        }

        Console.WriteLine($"\nAdded files ({syncResult.AddedFiles.Count}): ");
        foreach (var added in syncResult.AddedFiles)
        {
            Console.WriteLine(added.Path.SubPath);
        }

        Console.WriteLine($"\nDeleted files ({syncResult.DeletedFiles.Count}): ");
        foreach (var deleted in syncResult.DeletedFiles)
        {
            Console.WriteLine(deleted.Path.SubPath);
        }

        return 0;
    }
}
