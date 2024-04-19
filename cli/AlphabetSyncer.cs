using FishSyncClient.Downloader;
using FishSyncClient.FileComparers;
using FishSyncClient.Progress;
using FishSyncClient.Server;
using FishSyncClient.Syncer;
using FishSyncClient.Versions;

namespace FishSyncClient.Cli;

public class AlphabetSyncer
{
    private readonly HttpClient _httpClient = new();
    private readonly PathOptions _pathOptions = new();

    async Task Start()
    {
        var root = "C:/fishsync";
        var versionPath = "C:/fishsync.version.txt";
        var versionManager = new VersionManager(versionPath);

        var fileProgress = new SyncProgress<FishFileProgressEventArgs>(p => 
            Console.WriteLine($"[{p.EventType}] {p.ProgressedFiles}/{p.TotalFiles} {p.CurrentFileName}"));

        var lastByteProgress = new ByteProgress();
        var byteProgress = new SyncProgress<ByteProgress>(p => lastByteProgress = p);

        var serverIndex = await getServerIndex();
        var serverSyncer = new FishServerSyncer(
            versionManager, new DefaultFileComparerFactory(), new ParallelFileSyncer());
        var syncResult = await serverSyncer.Sync(
            serverIndex, getLocalPaths(root), fileProgress, default);

        foreach (var update in syncResult.UpdatedFiles)
        {
            Console.WriteLine($"Update {update.Path.SubPath}");
        }

        var downloader = new ParallelFileDownloader(_httpClient);
        var downloadTask = downloader.DownloadFiles(root, syncResult.UpdatedFiles, fileProgress, byteProgress, default);

        while (!downloadTask.IsCompleted)
        {
            var percent = lastByteProgress.GetPercentage().ToString("F");
            Console.WriteLine($"{percent}% ({lastByteProgress.ProgressedBytes} / {lastByteProgress.TotalBytes})");
            await Task.Delay(1000);
        }

        foreach (var delete in syncResult.DeletedFiles)
        {
            var path = delete.Path.WithRoot(root).GetFullPath();
            Console.WriteLine($"Delete {path}");
            File.Delete(path);
        }

        if (!syncResult.IsLatestVersion)
            await versionManager.UpdateVersion(syncResult.Version ?? "");

        Console.WriteLine("Done");
    }

    private async Task<FishServerSyncIndex> getServerIndex()
    {
        var metadata = await AlphabetFileUpdateServer.GetLauncherMetadata(
            _httpClient, 
            new Uri("http://15.165.76.11/launcher/files-al2.json"));
        return AlphabetFileUpdateServer.ToFishServerSyncIndex(metadata, _pathOptions);
    }

    IEnumerable<SyncFile> getLocalPaths(string root)
    {
        return RootedPath.FromDirectory(root, _pathOptions).Select(path =>
        {
            Console.WriteLine($"Local {path.SubPath}");
            return new SyncFile(path);
        });
    }


    void printDownloadFile(string root, IEnumerable<ServerSyncFile> files)
    {
        foreach (var file in files)
        {
            var fullPath = file.Path.WithRoot(root).GetFullPath();
            var location = file.Location;
            Console.WriteLine($"Download file {location} into {fullPath}");
        }
    }

    void printDeleteFile(string root, IEnumerable<SyncFile> paths)
    {
        foreach (var path in paths)
        {
            var fullPath = path.Path.WithRoot(root).GetFullPath();
            Console.WriteLine("Delete " + fullPath);
        }
    }
}
