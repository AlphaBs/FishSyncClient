using FishSyncClient.Syncer;

namespace FishSyncClient;

public class Class1
{
    async void a()
    {
        var root = "";
        var pathOptions = new PathOptions();
        var server = getServerFiles();
        var local = getLocalPaths(root, pathOptions);

        var pathSyncer = new FishPathSyncer();
        var pathSyncResult = pathSyncer.Sync(server, local); 

        var duplicatedFiles = pathSyncResult.DuplicatedPaths.Cast<FishFileMetadata>();
        var fileSyncer = new FishFileSyncer(new IFileComparer[] 
        { 
            new FileSizeComparer(), 
            new MD5FileComparer(), 
            new SHA1FileComparer() 
        });
        var fileSyncResult = await fileSyncer.Sync(root, duplicatedFiles);

        downloadFile(root, pathSyncResult.AddedPaths.Cast<FishServerFile>());
        downloadFile(root, fileSyncResult.UpdatedFiles.Cast<FishServerFile>());
        deleteFile(root, pathSyncResult.DeletedPaths);
    }

    IEnumerable<FishPath> getLocalPaths(string root, PathOptions pathOptions)
    {
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        foreach (var item in files)
        {
            var path = RootedPath.FromFullPath(root, item, pathOptions);
            yield return new FishPath(path);
        }
    }

    IEnumerable<FishServerFile> getServerFiles()
    {
        return Enumerable.Empty<FishServerFile>();
    }

    void downloadFile(string root, IEnumerable<FishServerFile> files)
    {
        foreach (var file in files)
        {
            var fullPath = file.Path.WithRoot(root).GetFullPath();
            var location = file.Location;

            Console.WriteLine($"Download file {location} into {fullPath}");
        }
    }

    void deleteFile(string root, IEnumerable<FishPath> paths)
    {
        foreach (var path in paths)
        {
            var fullPath = path.Path.WithRoot(root).GetFullPath();
            Console.WriteLine("Delete " + fullPath);
        }
    }
}
