using DotNet.Globbing;
using FishSyncClient.Files;
using FishSyncClient.FileComparers;
using FishSyncClient.Syncer;
using FishSyncClient.Progress;

namespace FishSyncClient;

public class SyncFileCollectionComparer
{
    private readonly ISyncFilePairCollectionComparer _fileSyncer;

    public SyncFileCollectionComparer(ISyncFilePairCollectionComparer fileSyncer) => 
        _fileSyncer = fileSyncer;

    public Task<SyncFileCollectionComparerResult> CompareFiles(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncerOptions? options)
    {
        options ??= new();
        return new SyncProcessor(_fileSyncer, options)
            .Sync(sources, targets, comparer); 
    }

    class SyncProcessor
    {
        private readonly ISyncFilePairCollectionComparer _fileSyncer;
        private readonly Glob[] _includesPatterns;
        private readonly Glob[] _excludesPatterns;
        private readonly SyncerOptions _options;

        public SyncProcessor(ISyncFilePairCollectionComparer fileSyncer, SyncerOptions options) 
        {
            _fileSyncer = fileSyncer;
            _options = options;
            _includesPatterns = parsePatternsToGlobs(options.Includes);
            _excludesPatterns = parsePatternsToGlobs(options.Excludes);
        }

        private Glob[] parsePatternsToGlobs(IEnumerable<string> patterns)
        {
            return patterns.Select(pattern => Glob.Parse(pattern)).ToArray();
        }

        public async Task<SyncFileCollectionComparerResult> Sync(
            IEnumerable<SyncFile> sources, 
            IEnumerable<SyncFile> targets, 
            IFileComparer comparer)
        {
            var pathSyncer = new SyncPathComparer();
            var pathSyncResult = pathSyncer.ComparePaths(sources, targets);

            var fileSyncResult = await _fileSyncer.ComparePairs(
                pairs: pathSyncResult.DuplicatedPaths, 
                comparer: comparer, 
                fileProgress: _options.FileProgress, 
                byteProgress: _options.ByteProgress,
                cancellationToken: _options.CancellationToken);

            return new SyncFileCollectionComparerResult(
                pathSyncResult.AddedPaths,
                fileSyncResult.UpdatedFiles.Where(pair => checkIncluded(pair.Source)).ToList(),
                fileSyncResult.IdenticalFiles.ToArray(),
                pathSyncResult.DeletedPaths.Where(checkIncluded).ToList());
        }

        private bool checkIncluded(SyncFile file)
        {
            return _includesPatterns.Any(pattern => pattern.IsMatch(file.Path.SubPath)) &&
                   !_excludesPatterns.Any(pattern => pattern.IsMatch(file.Path.SubPath));
        }
    }
}

public record SyncFileCollectionComparerResult(
    IReadOnlyCollection<SyncFile> AddedFiles,
    IReadOnlyCollection<SyncFilePair> UpdatedFilePairs,
    IReadOnlyCollection<SyncFilePair> IdenticalFilePairs,
    IReadOnlyCollection<SyncFile> DeletedFiles
);