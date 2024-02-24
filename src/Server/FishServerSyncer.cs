using FishSyncClient.FileComparers;
using FishSyncClient.Versions;

namespace FishSyncClient.Server;

public class FishServerSyncer
{
    private readonly IVersionManager _versionManager;

    public FishServerSyncer(IVersionManager versionManager) =>
        _versionManager = versionManager;

    public async Task<FishServerSyncResult> Sync(
        FishServerSyncIndex server,
        IEnumerable<SyncFile> targets,
        IProgress<FishFileProgressEventArgs>? progress,
        CancellationToken cancellationToken)
    {
        var newVersion = await _versionManager.CheckNewVersion(server.Version);

        var syncer = new FishSyncer();
        var comparer = createComparer(newVersion, server.SyncIncludes ?? Enumerable.Empty<string>());
        var syncResult = await syncer.Sync(server.Files, targets, comparer, new FishSyncOptions
        {
            SyncExcludes = server.SyncExcludes,
            Progress = progress,
            CancellationToken = default
        });

        var updatedFiles = syncResult.UpdatedFiles.Cast<ServerSyncFile>().ToArray();
        return new FishServerSyncResult(
            newVersion, 
            server.Version, 
            updatedFiles, 
            syncResult.DeletedFiles);
    }

    private IFileComparer createComparer(bool isNewVersion, IEnumerable<string> includePatterns)
    {
        if (isNewVersion)
        {
            return FileComparerFactory.CreateChecksumComparer();
        }
        else
        {
            var comparer = new CompositeFileComparerWithGlob();
            foreach (var includePattern in includePatterns)
            {
                comparer.Add(includePattern, FileComparerFactory.CreateChecksumComparer());
            }
            comparer.Add("**", FileComparerFactory.CreateSizeComparer());
            return comparer;
        }
    }

}

public class FishServerSyncResult
{
    public FishServerSyncResult(
        bool isLatest, 
        string? version,
        ServerSyncFile[] updatedFiles, 
        SyncFile[] deletedFiles)
    {
        IsLatestVersion = isLatest;
        Version = version;
        UpdatedFiles = updatedFiles;
        DeletedFiles = deletedFiles;
    }

    public bool IsLatestVersion { get; }
    public string? Version { get; }
    public ServerSyncFile[] UpdatedFiles { get; }
    public SyncFile[] DeletedFiles { get; }
}