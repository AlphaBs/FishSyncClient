using DotNet.Globbing;
using FishSyncClient.FileComparers;
using FishSyncClient.Syncer;

namespace FishSyncClient;

public class SyncOptions
{
    public IEnumerable<string> Includes { get; set; } = new [] { "**" };
    public IEnumerable<string> Excludes { get; set; } = Enumerable.Empty<string>();
    public IProgress<FishFileProgressEventArgs>? Progress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public class FishSyncer
{
    public Task<FishSyncResult> Sync(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncOptions options)
    {
        return new SyncProcessor(options).Sync(sources, targets, comparer); 
    }

    class SyncProcessor
    {
        private readonly Glob[] _includesPatterns;
        private readonly Glob[] _excludesPatterns;
        private readonly SyncOptions _options;

        public SyncProcessor(SyncOptions options) 
        {
            _options = options;
            _includesPatterns = parsePatternsToGlobs(options.Includes);
            _excludesPatterns = parsePatternsToGlobs(options.Excludes);
        }

        private Glob[] parsePatternsToGlobs(IEnumerable<string> patterns)
        {
            return patterns.Select(pattern => Glob.Parse(pattern)).ToArray();
        }

        public async Task<FishSyncResult> Sync(IEnumerable<SyncFile> sources, IEnumerable<SyncFile> targets, IFileComparer comparer)
        {
            var pathSyncer = new FishPathSyncer();
            var pathSyncResult = pathSyncer.Sync(sources, targets);

            var fileSyncer = new FishFileSyncer();
            var fileSyncResult = await fileSyncer.Sync(
                pairs: pathSyncResult.DuplicatedPaths, 
                comparer: comparer, 
                progress: _options.Progress, 
                cancellationToken: _options.CancellationToken);

            var updatedFiles = Enumerable.Concat(
                pathSyncResult.AddedPaths,
                fileSyncResult.UpdatedFiles.Select(pair => pair.Source));

            return new FishSyncResult(
                updatedFiles.Where(checkIncluded).ToArray(),
                fileSyncResult.IdenticalFiles.Select(pair => pair.Source).ToArray(),
                pathSyncResult.DeletedPaths.Where(checkIncluded).ToArray());
        }

        private bool checkIncluded(SyncFile file)
        {
            return _includesPatterns.Any(pattern => pattern.IsMatch(file.Path.SubPath)) &&
                   !_excludesPatterns.Any(pattern => pattern.IsMatch(file.Path.SubPath));
        }
    }
}

public class FishSyncResult
{
    public FishSyncResult(
        SyncFile[] updatedFiles,
        SyncFile[] identicalFiles,
        SyncFile[] deletedFiles) =>
        (UpdatedFiles, IdenticalFiles, DeletedFiles) =
        (updatedFiles, identicalFiles, deletedFiles);

    public SyncFile[] UpdatedFiles { get; }
    public SyncFile[] IdenticalFiles { get; }
    public SyncFile[] DeletedFiles { get; }
}