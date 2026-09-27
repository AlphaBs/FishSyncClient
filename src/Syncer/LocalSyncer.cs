using FishSyncClient.Files;
using FishSyncClient.Internals;

namespace FishSyncClient.Syncer;

public class LocalSyncer : SyncFileCollectionSyncer
{
    public static IEnumerable<SyncFile> EnumerateLocalSyncFiles(string root, PathOptions options)
    {
        return RootedPath.FromDirectory(root, options)
            .Select(path => new LocalSyncFile(path));
    }

    private readonly string _root;
    private readonly PathOptions _pathOptions;

    public LocalSyncer(
        string root,
        PathOptions pathOptions,
        ISyncFilePairSyncer syncer) : base(syncer, pathOptions)
    {
        _root = PathHelper.NormalizeRoot(root, pathOptions);
        LocalPathGuard.EnsureNoLinks(_root);
        _pathOptions = pathOptions;
    }

    public Task<SyncFileCollectionComparerResult> CompareFiles(
        IEnumerable<SyncFile> sources, 
        SyncerOptions? options)
    {
        var targets = EnumerateLocalSyncFiles(_root, _pathOptions);
        return CompareFiles(sources, targets, options);
    }

    public Task<SyncFileCollectionComparerResult> CompareAndSyncFiles(
        IEnumerable<SyncFile> sources,
        SyncerOptions? options)
    {
        var targets = EnumerateLocalSyncFiles(_root, _pathOptions);
        return CompareAndSyncFiles(sources, targets, options);
    }

    protected override IEnumerable<SyncFilePair> CreateFilePairs(IEnumerable<SyncFile> sourceFiles)
    {
        return sourceFiles.Select(source => 
            new SyncFilePair(
                source, 
                new LocalSyncFile(source.Path.WithRoot(_root))));
    }

    public void DeleteLocalFiles(IEnumerable<SyncFile> files)
    {
        foreach (var file in files)
        {
            if (file.Path.Root != _root)
                throw new InvalidOperationException("root not matched");
                
            var path = file.Path.GetFullPath();
            LocalPathGuard.EnsureNoLinks(path);
            File.Delete(path);
        }
    }
}
