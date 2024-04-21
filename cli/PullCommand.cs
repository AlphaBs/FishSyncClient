using CommandLine;
using FishSyncClient.FileComparers;
using FishSyncClient.Progress;
using FishSyncClient.Server;
using FishSyncClient.Syncer;
using FishSyncClient.Versions;

namespace FishSyncClient.Cli;

[Verb("pull")]
public class PullCommand : CommandBase
{
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
        var pathOptions = new PathOptions();

        var apiClient = new FishApiClient(host, httpClient);
        var bucketFiles = await apiClient.GetBucketFiles(Id);
        var syncFiles = bucketFiles.GetSyncFiles(httpClient, pathOptions);

        var progressAggregator = new ConcurrentByteProgressAggregator();
        var fileProgress = new SyncProgress<FileProgressEvent>(e =>
        {
            Console.WriteLine($"[{e.EventType}][{e.ProgressedFiles}/{e.TotalFiles}] {e.CurrentFileName}");
        });
        var byteProgress = new SyncProgress<SyncFileByteProgress>(e =>
        {
            progressAggregator.Report(e.Progress);
        });

        var syncer = new LocalSyncer(
            Root,
            pathOptions,
            6,
            new NullVersionManager(),
            new DefaultFileComparerFactory(),
            new ParallelSyncFilePairComparer());

        var syncTask = syncer.Sync(syncFiles, new SyncerOptions
        {
            FileProgress = fileProgress,
            ByteProgress = byteProgress
        });

        while (!syncTask.IsCompleted)
        {
            await Task.WhenAny(syncTask, Task.Delay(100));
            var progress = progressAggregator.AggregateProgress();
            Console.WriteLine($"{progress.GetPercentage(false):p} ( {progress.ProgressedBytes:#,##} / {progress.TotalBytes:#,##} )");
        }

        var syncResult = await syncTask;
        Console.WriteLine($"\n동일 파일({syncResult.CompareResult.IdenticalFilePairs.Count}): ");
        foreach (var identical in syncResult.CompareResult.IdenticalFilePairs)
        {
            Console.WriteLine(identical.Source.Path.SubPath);
        }

        Console.WriteLine($"\n업데이트 파일({syncResult.CompareResult.UpdatedFilePairs.Count}): ");
        foreach (var updated in syncResult.CompareResult.UpdatedFilePairs)
        {
            Console.WriteLine(updated.Source.Path.SubPath);
        }

        Console.WriteLine($"\n추가된 파일({syncResult.CompareResult.AddedFiles.Count}): ");
        foreach (var added in syncResult.CompareResult.AddedFiles)
        {
            Console.WriteLine(added.Path.SubPath);
        }

        Console.WriteLine($"\n삭제된 파일({syncResult.CompareResult.DeletedFiles.Count}): ");
        foreach (var deleted in syncResult.CompareResult.DeletedFiles)
        {
            Console.WriteLine(deleted.Path.SubPath);
        }

        return 0;
    }
}