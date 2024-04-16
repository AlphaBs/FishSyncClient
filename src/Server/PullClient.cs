using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Syncer;
using FishSyncClient.Versions;

namespace FishSyncClient.Server;

public class PullIndex
{
    public string? Version { get; set; }
    public SyncFile[] Files { get; set; } = [];
    public IEnumerable<string> SyncExcludes { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<string> SyncIncludes { get; set; } = new [] { "**" };
}

public class PullClient
{
    private readonly IVersionManager _versionManager;
    private readonly IFileComparerFactory _comparerFactory;
    private readonly IFishFileSyncer _fileSyncer;

    public PullClient(
        IVersionManager versionManager, 
        IFileComparerFactory comparerFactory,
        IFishFileSyncer fileSyncer) =>
        (_versionManager, _comparerFactory, _fileSyncer) = 
        (versionManager, comparerFactory, fileSyncer);

    public async Task<PullResult> Pull(
        PullIndex server,
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

        var updatedFiles = syncResult.UpdatedFiles.Cast<ReadableHttpSyncFile>().ToArray();
        return new PullResult(
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

public record PullResult(
    bool IsLatestVersion,
    string? Version,
    IReadOnlyCollection<SyncFile> UpdatedFiles,
    IReadOnlyCollection<SyncFile> DeletedFiles
);