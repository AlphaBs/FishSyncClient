using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;

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
    private readonly ISyncFilePairCollectionComparer _filePairComparer;

    public LocalSyncer(
        string root,
        PathOptions pathOptions,
        int maxParallelism,
        ISyncFilePairCollectionComparer filePairComparer) =>
        (_root, _pathOptions, _maxParallelism, _filePairComparer) =
        (root, pathOptions, maxParallelism, filePairComparer);

    public Task<SyncFileCollectionComparerResult> Sync(
        IEnumerable<SyncFile> sources, 
        IFileComparer comparer,
        SyncerOptions? options)
    {
        var targets = EnumerateLocalSyncFiles(_root, _pathOptions);
        return Sync(sources, targets, comparer, options);
    }

    public async Task<SyncFileCollectionComparerResult> Sync(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncerOptions? options)
    {
        options ??= new();

        // sources 와 targets 비교
        var fileCollectionComparer = new SyncFileCollectionComparer(_filePairComparer);
        var syncResult = await fileCollectionComparer.CompareFiles(
            sources, 
            targets, 
            comparer, 
            new SyncFileCollectionComparerOptions
            {
                Includes = options.Includes,
                Excludes = options.Excludes,
                FileProgress = options.FileProgress,
                ByteProgress = options.ByteProgress,
                CancellationToken = options.CancellationToken
            });

        // AddedFiles 과 대응되는 LocalFile 만들어서 SyncFilePair 만들기
        var addedFilePairs = syncResult.AddedFiles.Select(
            file => new SyncFilePair(file, createLocalFile(file)));

        // SyncFilePair 모두 동기화
        var filePairSyncer = new FilePairSyncer(_maxParallelism);
        await filePairSyncer.SyncFilePairs(
            syncResult.UpdatedFilePairs.Concat(addedFilePairs), 
            options);

        // DeletedFiles 삭제
        deleteFiles(syncResult.DeletedFiles);

        return syncResult;
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