using FishSyncClient.FileComparers;
using FishSyncClient.Versions;

namespace FishSyncClient.Server.Alphabet;

public class AlphabetSyncer
{
    private readonly IVersionManager _versionManager;
    private readonly PathOptions _pathOptions;

    public AlphabetSyncer(
        IVersionManager versionManager, 
        PathOptions pathOptions) =>
        (_versionManager, _pathOptions) = 
        (versionManager, pathOptions);

    public async Task<AlphabetSyncResult> Sync(
        LauncherMetadata server,
        IEnumerable<FishPath> targets,
        IProgress<FishFileProgressEventArgs>? progress,
        CancellationToken cancellationToken)
    {
        if (server.Files == null)
            throw new ArgumentException();

        var serverVersion = server.Files.LastUpdate.ToString("o");
        var newVersion = await _versionManager.CheckNewVersion(serverVersion);

        var serverFiles = AlphabetFileUpdateServer.ToFishServerFiles(server.Files, _pathOptions);
        var comparer = createComparer(newVersion, server.Launcher?.IncludeFiles ?? Enumerable.Empty<string>());

        var syncer = new FishSyncer();
        var syncResult = await syncer.Sync(serverFiles, targets, comparer, new FishSyncOptions
        {
            UpdateExcludes = server.Launcher?.WhitelistFiles ?? Enumerable.Empty<string>(),
            PathOptions = _pathOptions,
            Progress = progress,
            CancellationToken = default
        });

        var updatedFiles = syncResult.UpdatedFiles.Cast<FishServerFile>().ToArray();
        return new AlphabetSyncResult(
            newVersion, 
            serverVersion, 
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
            comparer.Add("**", FileComparerFactory.CreateChecksumComparer());
            return comparer;
        }
    }

}

public class AlphabetSyncResult
{
    public AlphabetSyncResult(
        bool isLatest, 
        string version,
        FishServerFile[] updatedFiles, 
        FishPath[] deletedFiles)
    {
        IsLatestVersion = isLatest;
        Version = version;
        UpdatedFiles = updatedFiles;
        DeletedFiles = deletedFiles;
    }

    public bool IsLatestVersion { get; }
    public string Version { get; }
    public FishServerFile[] UpdatedFiles { get; }
    public FishPath[] DeletedFiles { get; }
}