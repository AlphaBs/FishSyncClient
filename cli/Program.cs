using FishSyncClient.Downloader;
using FishSyncClient.Server;
using FishSyncClient.Server.Alphabet;
using FishSyncClient.Syncer;
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

        ByteProgress lastByteProgress = new ByteProgress();
        var byteProgress = new SyncProgress<ByteProgress>(p => lastByteProgress = p);

        var serverMetadata = await getServerFiles();
        if (serverMetadata.Files == null)
            throw new Exception();
        var serverVersion = serverMetadata.Files.LastUpdate.ToString("o");
        var newVersion = await versionManager.CheckNewVersion(serverVersion);
        Console.WriteLine("newVersion? " + newVersion);

        var server = AlphabetFileUpdateServer.ToFishServerFiles(serverMetadata.Files, _pathOptions);
        var local = getLocalPaths(root);

        var pathSyncer = new FishPathSyncer();
        var pathSyncResult = pathSyncer.Sync(server, local);
        var duplicatedFiles = pathSyncResult.DuplicatedPaths.Cast<FishFileMetadata>();

        var fileSyncer = newVersion
            ? FishFileSyncerFactory.CreateWithChecksumComparer()
            : FishFileSyncerFactory.CreateWithSizeComparer();
        var fileSyncResult = await fileSyncer.Sync(root, duplicatedFiles, fileProgress);

        var downloadFiles = Enumerable.Concat(
            pathSyncResult.AddedPaths.Cast<FishServerFile>(),
            fileSyncResult.UpdatedFiles.Cast<FishServerFile>());

        var downloader = new SequentialFileDownloader(_httpClient);
        var downloadTask = downloader.DownloadFiles(root, downloadFiles, fileProgress, byteProgress, default);

        while (!downloadTask.IsCompleted)
        {
            Console.WriteLine((int)lastByteProgress.GetPercentage() + "%");
            await Task.Delay(1000);
        }

        foreach (var delete in pathSyncResult.DeletedPaths)
        {
            var path = delete.Path.WithRoot(root).GetFullPath();
            Console.WriteLine($"Delete {path}");
            File.Delete(path);
        }

        if (newVersion)
            await versionManager.UpdateVersion(serverVersion);

        Console.WriteLine("Done");
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

    async Task<LauncherMetadata> getServerFiles()
    {
        var metadata = await AlphabetFileUpdateServer.GetLauncherMetadata(
            _httpClient, 
            new Uri("http://15.165.76.11/launcher/files-al2.json"));

        return metadata;
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
