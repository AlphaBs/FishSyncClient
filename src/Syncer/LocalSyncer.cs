using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Versions;
using System.Threading.Tasks.Dataflow;

namespace FishSyncClient.Syncer;

public class SyncerOptions
{
    public string? Version { get; set; }
    public IEnumerable<string> Excludes { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<string> Includes { get; set; } = ["**"];
    public IProgress<FileProgressEvent>? FileProgress { get; set; }
    public IProgress<SyncFileByteProgress>? ByteProgress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public class LocalSyncer
{
    public static IEnumerable<SyncFile> EnumerateLocalSyncFiles(string root, PathOptions options)
    {
        return RootedPath.FromDirectory(root, options)
            .Select(path => new LocalSyncFile(path));
    }

    private readonly string _root;
    private readonly PathOptions _pathOptions;
    private readonly int _maxParallelism;
    private readonly IVersionManager _versionManager;
    private readonly IFileComparerFactory _comparerFactory;
    private readonly ISyncFilePairComparer _fileSyncer;

    public LocalSyncer(
        string root,
        PathOptions pathOptions,
        int maxParallelism,
        IVersionManager versionManager,
        IFileComparerFactory comparerFactory,
        ISyncFilePairComparer fileSyncer) =>
        (_root, _pathOptions, _maxParallelism, _versionManager, _comparerFactory, _fileSyncer) =
        (root, pathOptions, maxParallelism, versionManager, comparerFactory, fileSyncer);

    public Task<SyncResult> Sync(IEnumerable<SyncFile> sources, SyncerOptions? options)
    {
        var targets = EnumerateLocalSyncFiles(_root, _pathOptions);
        return Sync(sources, targets, options);
    }

    public async Task<SyncResult> Sync(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        SyncerOptions? options)
    {
        options ??= new();
        var newVersion = await _versionManager.CheckNewVersion(options.Version);

        var syncer = new SyncFileComparer(_fileSyncer);
        var comparer = createComparer(newVersion, options.Includes);
        var syncResult = await syncer.CompareFiles(sources, targets, comparer, new SyncFileComparerOptions
        {
            Includes = options.Includes,
            Excludes = options.Excludes,
            FileProgress = options.FileProgress,
            ByteProgress = options.ByteProgress,
            CancellationToken = options.CancellationToken
        });

        await syncFilePairs(syncResult, options);
        deleteFiles(syncResult.DeletedFiles);

        return new SyncResult(
            newVersion,
            options.Version,
            syncResult);
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

    private async Task syncFilePairs(SyncFileCompareResult syncResult, SyncerOptions options)
    {
        var addedFilePairs = syncResult.AddedFiles.Select(
            file => new SyncFilePair(file, createLocalFile(file)));
        var syncPairs = syncResult.UpdatedFilePairs.Concat(addedFilePairs);

        int total = syncResult.AddedFiles.Count + syncResult.UpdatedFilePairs.Count;
        int progressed = 0;

        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            options.FileProgress?.Report(new FileProgressEvent(FileProgressEventType.StartSync, progressed, total, pair.Source.Path.SubPath));
            await pair.SyncContent(options.ByteProgress, options.CancellationToken);

            Interlocked.Increment(ref progressed);
            options.FileProgress?.Report(new FileProgressEvent(FileProgressEventType.DoneSync, progressed, total, pair.Source.Path.SubPath));
        }, new ExecutionDataflowBlockOptions
        {
            CancellationToken = options.CancellationToken,
            EnsureOrdered = false,
            MaxDegreeOfParallelism = _maxParallelism
        });

        foreach (var pair in syncPairs)
        {
            options.FileProgress?.Report(new FileProgressEvent(FileProgressEventType.Queue, progressed, total, pair.Source.Path.SubPath));
            options.ByteProgress?.Report(
                new SyncFileByteProgress(
                    pair.Source, 
                    new ByteProgress 
                    { 
                        TotalBytes = pair.Source.Metadata?.Size ?? 0, 
                        ProgressedBytes = 0
                    }));

            await block.SendAsync(pair);
        }
        block.Complete();
        await block.Completion;
    }

    private LocalSyncFile createLocalFile(SyncFile file)
    {
        var newPath = file.Path.WithRoot(_root);
        return new LocalSyncFile(newPath);
    }

    private void deleteFiles(IEnumerable<SyncFile> files)
    {
        foreach (var file in files)
        {
            var path = file.Path.GetFullPath();
            File.Delete(path);
        }
    }
}

public record SyncResult(
    bool IsLatestVersion,
    string? Version,
    SyncFileCompareResult CompareResult
);