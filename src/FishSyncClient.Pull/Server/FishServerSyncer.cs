using FishSyncClient.FileComparers;
using FishSyncClient.Syncer;
using FishSyncClient.Versions;

namespace FishSyncClient.Server;

public class FishServerSyncer
{
    private readonly IVersionManager _versionManager;
    private readonly IFileComparerFactory _comparerFactory;
    private readonly IFishFileSyncer _fileSyncer;

    public FishServerSyncer(
        IVersionManager versionManager, 
        IFileComparerFactory comparerFactory,
        IFishFileSyncer fileSyncer) =>
        (_versionManager, _comparerFactory, _fileSyncer) = 
        (versionManager, comparerFactory, fileSyncer);

    public async Task<FishServerSyncResult> Sync(
        FishServerSyncIndex server,
        IEnumerable<SyncFile> targets,
        IProgress<FishFileProgressEventArgs>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var newVersion = await _versionManager.CheckNewVersion(server.Version);

        var syncer = new FishSyncer(_fileSyncer);
        var comparer = createComparer(newVersion, server.SyncIncludes);
        var syncResult = await syncer.Sync(server.Files, targets, comparer, new SyncOptions
        {
            Includes = server.SyncIncludes,
            Excludes = server.SyncExcludes,
            Progress = progress,
            CancellationToken = cancellationToken
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
            return _comparerFactory.CreateFullComparer();
        }
        else
        {
            var comparer = new CompositeFileComparerWithGlob();
            foreach (var includePattern in includePatterns)
            {
                comparer.Add(includePattern, _comparerFactory.CreateFullComparer());
            }
            comparer.Add("**", _comparerFactory.CreateFastComparer());
            return comparer;
        }
    }

}

public record FishServerSyncResult(
    bool IsLatestVersion,
    string? Version,
    IReadOnlyCollection<ServerSyncFile> UpdatedFiles,
    IReadOnlyCollection<SyncFile> DeletedFiles
);