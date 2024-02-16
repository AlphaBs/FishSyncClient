using FishSyncClient.FileComparers;
using FishSyncClient.Syncer;

namespace FishSyncClient;

public class FishSyncOptions
{
    public IEnumerable<string> UpdateExcludes { get; set; } = Enumerable.Empty<string>();
    public PathOptions PathOptions { get; set; } = new();
    public IProgress<FishFileProgressEventArgs>? Progress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public class FishSyncer
{
    public async Task<FishSyncResult> Sync(
        string root,
        IEnumerable<FishFileMetadata> sources, 
        IEnumerable<FishPath> targets, 
        IFileComparer comparer,
        FishSyncOptions options)
    {
        var pathSyncer = new FishPathSyncer(options.UpdateExcludes);
        var pathSyncResult = pathSyncer.Sync(sources, targets);
        var duplicatedFiles = pathSyncResult.DuplicatedPaths.Cast<FishFileMetadata>();
        
        var fileSyncer = new FishFileSyncer();
        var fileSyncResult = await fileSyncer.Sync(root, duplicatedFiles, comparer, options.Progress);

        var updatedFiles = Enumerable.Concat(
            pathSyncResult.AddedPaths.Cast<FishFileMetadata>(),
            fileSyncResult.UpdatedFiles.Cast<FishFileMetadata>());

        return new FishSyncResult(
            updatedFiles.ToArray(), 
            fileSyncResult.IdenticalFiles.ToArray(), 
            pathSyncResult.DeletedPaths.ToArray());
    }
}

public class FishSyncResult
{
    public FishSyncResult(
        FishFileMetadata[] updatedFiles, 
        FishFileMetadata[] identicalFiles, 
        FishPath[] deletedFiles) =>
        (UpdatedFiles, IdenticalFiles, DeletedFiles) = 
        (updatedFiles, identicalFiles, deletedFiles);

    public FishFileMetadata[] UpdatedFiles { get; }
    public FishFileMetadata[] IdenticalFiles { get;}
    public FishPath[] DeletedFiles { get; }
}