using CommandLine;
using FishSyncClient.FileComparers;
using FishSyncClient.Progress;
using FishSyncClient.Server;
using FishSyncClient.Syncer;
using FishSyncClient.Versions;
using System.Diagnostics;

namespace FishSyncClient.Cli;

[Verb("alphabet")]
public class AlphabetCommand : CommandBase
{
    protected override async ValueTask<int> RunAsync()
    {
        if (string.IsNullOrEmpty(Root))
            Root = Environment.CurrentDirectory;

        var host = GetHost();
        if (string.IsNullOrEmpty(host))
            throw new ArgumentException("host");

        var httpClient = new HttpClient();
        var pathOptions = new PathOptions();
        var metadata = await AlphabetFileUpdateServer.GetLauncherMetadata(httpClient, new Uri(host));
        var syncFiles = metadata.GetSyncFiles(httpClient, pathOptions);

        var progressAggregator = new ConcurrentByteProgressAggregator();
        var fileProgress = new SyncProgress<FileProgressEvent>(e =>
        {
            Console.WriteLine($"[{e.EventType}][{e.ProgressedFiles}/{e.TotalFiles}] {e.CurrentFileName}");
        });
        var byteProgress = new SyncProgress<SyncFileByteProgress>(e =>
        {
            progressAggregator.Report(e.Progress);
        });

        var versionManager = new VersionManager("version.txt");
        var isNewVersion = await versionManager.CheckNewVersion(metadata.LastInfoUpdate.ToString("o"));
        var comparerFactory = new LocalFileComparerFactory();
        IFileComparer comparer;
        if (isNewVersion)
        {
            comparer = comparerFactory.CreateFullComparer();
        }
        else
        {
            var comparerWithGlob = new CompositeFileComparerWithGlob();
            foreach (var pattern in metadata.Launcher?.IncludeFiles ?? [])
            {
                comparerWithGlob.Add(pattern, comparerFactory.CreateFullComparer());
            }
            comparerWithGlob.Add("**", comparerFactory.CreateFastComparer());
            comparer = comparerWithGlob;
        }

        var syncer = new LocalSyncer(
            Root,
            pathOptions,
            new ParallelSyncFilePairSyncer());

        var sw = new Stopwatch();
        sw.Start();
        var syncTask = syncer.CompareAndSyncFiles(syncFiles, comparer, new SyncerOptions
        {
            FileProgress = fileProgress,
            ByteProgress = byteProgress
        });

        while (!syncTask.IsCompleted)
        {
            await Task.WhenAny(syncTask, Task.Delay(100));
            printByteProgress(progressAggregator);
        }
        sw.Stop();
        printByteProgress(progressAggregator);

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
        syncer.DeleteLocalFiles(syncResult.DeletedFiles);
        foreach (var deleted in syncResult.DeletedFiles)
        {
            Console.WriteLine(deleted.Path.SubPath);
        }

        Console.WriteLine("\nNewVersion: " + isNewVersion);
        if (isNewVersion)
        {
            await versionManager.UpdateVersion(metadata.LastInfoUpdate.ToString("o"));
        }
        Console.WriteLine("ElapsedSeconds: " + sw.Elapsed.TotalSeconds);

        return 0;
    }

    private void printByteProgress(ConcurrentByteProgressAggregator progressAggregator)
    {
        var progress = progressAggregator.AggregateProgress();
        Console.WriteLine($"{progress.GetRatio():p} ( {progress.ProgressedBytes:#,##} / {progress.TotalBytes:#,##} )");
    }
}
