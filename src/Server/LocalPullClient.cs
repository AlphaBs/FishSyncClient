using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Internals;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;
using FishSyncClient.Versions;
using System.Threading.Tasks.Dataflow;

namespace FishSyncClient.Server;

public class PullIndex
{
    public string? Version { get; set; }
    public IEnumerable<SyncFile> Files { get; set; } = [];
    public IEnumerable<string> Excludes { get; set; } = Enumerable.Empty<string>();
    public IEnumerable<string> Includes { get; set; } = ["**"];
}

public class LocalPullClient
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
    private readonly IFishFileSyncer _fileSyncer;

    public LocalPullClient(
        string root,
        PathOptions pathOptions,
        int maxParallelism,
        IVersionManager versionManager, 
        IFileComparerFactory comparerFactory,
        IFishFileSyncer fileSyncer) =>
        (_root, _pathOptions, _maxParallelism, _versionManager, _comparerFactory, _fileSyncer) = 
        (root, pathOptions, maxParallelism, versionManager, comparerFactory, fileSyncer);

    public Task<PullResult> Pull(PullIndex index, SyncOptions? options)
    {
        var targets = EnumerateLocalSyncFiles(_root, _pathOptions);
        return Pull(index, targets, options);
    }

    public async Task<PullResult> Pull(
        PullIndex index,
        IEnumerable<SyncFile> targets,
        SyncOptions? options)
    {
        options ??= new();
        options.Excludes = index.Excludes;
        options.Includes = index.Includes;

        var newVersion = await _versionManager.CheckNewVersion(index.Version);

        var syncer = new FishSyncer(_fileSyncer);
        var comparer = createComparer(newVersion, index.Includes);
        var syncResult = await syncer.Sync(index.Files, targets, comparer, options);

        await syncFilePairs(syncResult, options);
        deleteFiles(syncResult.DeletedFiles);

        return new PullResult(
            newVersion, 
            index.Version,
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

    private async Task syncFilePairs(FishSyncResult syncResult, SyncOptions options)
    {
        var addedFilePairs = syncResult.AddedFiles.Select(
            file => new SyncFilePair(file, createLocalFile(file)));
        var syncPairs = syncResult.UpdatedFilePairs.Concat(addedFilePairs);

        int total = syncResult.AddedFiles.Count + syncResult.UpdatedFilePairs.Count;
        int progressed = 0;

        var block = new ActionBlock<SyncFilePair>(async pair =>
        {
            options.FileProgress?.Report(new FishFileProgressEventArgs(FishFileProgressEventType.StartSync, progressed, total, pair.Source.Path.SubPath));
            await pair.SyncContent(options.ByteProgress, options.CancellationToken);

            Interlocked.Increment(ref progressed);
            options.FileProgress?.Report(new FishFileProgressEventArgs(FishFileProgressEventType.DoneSync, progressed, total, pair.Source.Path.SubPath));
        }, new ExecutionDataflowBlockOptions
        {
            CancellationToken = options.CancellationToken,
            EnsureOrdered = false,
            MaxDegreeOfParallelism = _maxParallelism
        });
        
        foreach (var pair in syncPairs)
        {
            options.ByteProgress?.Report(new SyncFileByteProgress(pair.Source, new ByteProgress
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

public record PullResult(
    bool IsLatestVersion,
    string? Version,
    FishSyncResult SyncResult
);