using FishBucket;
using FishBucket.ApiClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;
using SyncAction = FishSyncClient.Syncer.SyncAction;

namespace FishSyncClient.Cli;

public class PullCommand : SyncCommandBase
{
    protected override async ValueTask<int> RunAsync()
    {
        var settings = await GetSettings(BucketId);

        using var httpClient = CreateHttpClient();
        using var apiHttpClient = CreateHttpClient();
        var pathOptions = new PathOptions();

        var apiClient = await CreateApiClient(settings, apiHttpClient);
        var bucketFiles = await apiClient.GetBucketFiles(settings.BucketId);
        var syncFiles = bucketFiles.Files
            .Select(file => CliSyncFiles.CreateHttpFile(file, httpClient, pathOptions))
            .ToArray();

        // 내려받을 파일과 덮어쓰기·삭제될 수 있는 로컬 파일을 함께 확인한다.
        var localPaths = RootedPath.FromDirectory(settings.Root, pathOptions)
            .Select(path => path.SubPath);
        var affectedPaths = syncFiles
            .Select(file => file.Path.SubPath)
            .Concat(localPaths);
        if (!ConfirmWarnings(affectedPaths, settings))
        {
            return 1;
        }

        var progressAggregator = new ConcurrentByteProgressAggregator();
        var fileProgress = new SyncProgress<FileProgressEvent>(e =>
        {
            Output.WriteLine($"[{e.EventType}][{e.ProgressedFiles}/{e.TotalFiles}] {e.CurrentFileName}");
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

        var syncTask = syncer.CompareAndSyncFiles(syncFiles, new SyncerOptions
        {
            Rules = [new SyncRule(SyncAction.FullSync, SyncCondition.Always, "**", comparerFactory.CreateFullComparer())],
            FileProgress = fileProgress,
            ByteProgress = byteProgress
        });

        while (!syncTask.IsCompleted)
        {
            await Task.WhenAny(syncTask, Task.Delay(100));
            var progress = progressAggregator.AggregateProgress();
            Output.WriteLine($"{progress.GetRatio():p} ( {progress.ProgressedBytes:#,##} / {progress.TotalBytes:#,##} )");
        }

        var syncResult = await syncTask;
        Output.WriteLine($"\nIdentical files ({syncResult.IdenticalFilePairs.Count}): ");
        foreach (var identical in syncResult.IdenticalFilePairs)
        {
            Output.WriteLine(identical.Source.Path.SubPath);
        }

        Output.WriteLine($"\nUpdated files ({syncResult.UpdatedFilePairs.Count}): ");
        foreach (var updated in syncResult.UpdatedFilePairs)
        {
            Output.WriteLine(updated.Source.Path.SubPath);
        }

        Output.WriteLine($"\nAdded files ({syncResult.AddedFiles.Count}): ");
        foreach (var added in syncResult.AddedFiles)
        {
            Output.WriteLine(added.Path.SubPath);
        }

        Output.WriteLine($"\nDeleted files ({syncResult.DeletedFiles.Count}): ");
        foreach (var deleted in syncResult.DeletedFiles)
        {
            Output.WriteLine(deleted.Path.SubPath);
        }

        return 0;
    }
}
