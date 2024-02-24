using FishSyncClient.FileComparers;
using FishSyncClient.Syncer;

namespace FishSyncClient;

public class FishSyncOptions
{
    public IEnumerable<string> SyncExcludes { get; set; } = Enumerable.Empty<string>();
    public IProgress<FishFileProgressEventArgs>? Progress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public class FishSyncer
{
    public async Task<FishSyncResult> Sync(
        IEnumerable<FishPath> sources, 
        IEnumerable<FishPath> targets, 
        IFileComparer comparer,
        FishSyncOptions options)
    {
        var pathSyncer = new FishPathSyncer(options.SyncExcludes);
        var pathSyncResult = pathSyncer.Sync(sources, targets);
        
        var fileSyncer = new FishFileSyncer();
        var fileSyncResult = await fileSyncer.Sync(pathSyncResult.DuplicatedPaths, comparer, options.Progress);

        var updatedFiles = Enumerable.Concat(
            pathSyncResult.AddedPaths,
            fileSyncResult.UpdatedFiles.Select(pair => pair.Source));

        return new FishSyncResult(
            updatedFiles.ToArray(), 
            fileSyncResult.IdenticalFiles.Select(pair => pair.Source).ToArray(), 
            pathSyncResult.DeletedPaths.ToArray());
    }
}

public class FishSyncResult
{
    public FishSyncResult(
        FishPath[] updatedFiles, 
        FishPath[] identicalFiles, 
        FishPath[] deletedFiles) =>
        (UpdatedFiles, IdenticalFiles, DeletedFiles) = 
        (updatedFiles, identicalFiles, deletedFiles);

    public FishPath[] UpdatedFiles { get; }
    public FishPath[] IdenticalFiles { get;}
    public FishPath[] DeletedFiles { get; }
}