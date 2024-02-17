using FishSyncClient.Downloader;
using FishSyncClient.Server;
using FishSyncClient.Server.Alphabet;
using FishSyncClient.Versions;

namespace FishSyncClient.Cli;

public class Program
{
    public static async Task Main()
    {
        var program = new Program();
        await program.Start();
    }

    private readonly HttpClient _httpClient = new();
    private readonly PathOptions _pathOptions = new();

    async Task Start()
    {
        var root = Environment.GetEnvironmentVariable("APPDATA") + "/ICY_ONLINE/game";
        var versionPath = Environment.GetEnvironmentVariable("APPDATA") + "/ICY_ONLINE/version.dat";
        var versionManager = new VersionManager(versionPath);

        var fileProgress = new SyncProgress<FishFileProgressEventArgs>(p => 
            Console.WriteLine($"{p.ProgressedFiles}/{p.TotalFiles} {p.CurrentFile.SubPath}"));

        var lastByteProgress = new ByteProgress();
        var byteProgress = new SyncProgress<ByteProgress>(p => lastByteProgress = p);

        var alphabetServer = await getServerFiles();
        var alphabetSyncer = new AlphabetSyncer(versionManager, _pathOptions);
        var syncResult = await alphabetSyncer.Sync(
            alphabetServer, getLocalPaths(root), fileProgress, default);

        var downloader = new SequentialFileDownloader(_httpClient);
        var downloadTask = downloader.DownloadFiles(root, syncResult.UpdatedFiles, fileProgress, byteProgress, default);

        while (!downloadTask.IsCompleted)
        {
            Console.WriteLine((int)lastByteProgress.GetPercentage() + "%");
            await Task.Delay(1000);
        }

        foreach (var delete in syncResult.DeletedFiles)
        {
            var path = delete.Path.WithRoot(root).GetFullPath();
            Console.WriteLine($"Delete {path}");
            File.Delete(path);
        }

        if (!syncResult.IsLatestVersion)
            await versionManager.UpdateVersion(syncResult.Version);

        Console.WriteLine("Done");
    }

    private async Task<LauncherMetadata> getServerFiles()
    {
        var metadata = await AlphabetFileUpdateServer.GetLauncherMetadata(
            _httpClient, 
            new Uri("http://15.165.76.11/launcher/files-al2.json"));

        return metadata;
    }

    IEnumerable<FishPath> getLocalPaths(string root)
    {
        if (!Directory.Exists(root))
            yield break;

        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        foreach (var item in files)
        {
            var path = RootedPath.FromFullPath(root, item, _pathOptions);
            Console.WriteLine($"Local {path.SubPath}");
            yield return new FishPath(path);
        }
    }


    void printDownloadFile(string root, IEnumerable<FishServerFile> files)
    {
        foreach (var file in files)
        {
            var fullPath = file.Path.WithRoot(root).GetFullPath();
            var location = file.Location;
            Console.WriteLine($"Download file {location} into {fullPath}");
        }
    }

    void printDeleteFile(string root, IEnumerable<FishPath> paths)
    {
        foreach (var path in paths)
        {
            var fullPath = path.Path.WithRoot(root).GetFullPath();
            Console.WriteLine("Delete " + fullPath);
        }
    }
}
