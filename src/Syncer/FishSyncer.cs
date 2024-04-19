using DotNet.Globbing;
using FishSyncClient.Files;
using FishSyncClient.FileComparers;
using FishSyncClient.Syncer;
using FishSyncClient.Progress;

namespace FishSyncClient;

public class SyncOptions
{
    public IEnumerable<string> Includes { get; set; } = ["**"];
    public IEnumerable<string> Excludes { get; set; } = Enumerable.Empty<string>();
    public IProgress<FishFileProgressEventArgs>? FileProgress { get; set; }
    public IProgress<SyncFileByteProgress>? ByteProgress { get; set; }
    public CancellationToken CancellationToken { get; set; }
}

public class FishSyncer
{
    private readonly IFishFileSyncer _fileSyncer;

    public FishSyncer(IFishFileSyncer fileSyncer) => 
        _fileSyncer = fileSyncer;

    public Task<FishSyncResult> Sync(
        IEnumerable<SyncFile> sources,
        IEnumerable<SyncFile> targets,
        IFileComparer comparer,
        SyncOptions? options)
    {
        options ??= new();
        return new SyncProcessor(_fileSyncer, options)
            .Sync(sources, targets, comparer); 
    }

    class SyncProcessor
    {
        private readonly IFishFileSyncer _fileSyncer;
        private readonly Glob[] _includesPatterns;
        private readonly Glob[] _excludesPatterns;
        private readonly SyncOptions _options;

        public SyncProcessor(IFishFileSyncer fileSyncer, SyncOptions options) 
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

        public async Task<FishSyncResult> Sync(
            IEnumerable<SyncFile> sources, 
            IEnumerable<SyncFile> targets, 
            IFileComparer comparer)
        {
            var pathSyncer = new FishPathSyncer();
            var pathSyncResult = pathSyncer.Sync(sources, targets);

            var fileSyncResult = await _fileSyncer.Sync(
                pairs: pathSyncResult.DuplicatedPaths, 
                comparer: comparer, 
                fileProgress: _options.FileProgress, 
                byteProgress: _options.ByteProgress,
                cancellationToken: _options.CancellationToken);

            return new FishSyncResult(
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

public record FishSyncResult(
    IReadOnlyCollection<SyncFile> AddedFiles,
    IReadOnlyCollection<SyncFilePair> UpdatedFilePairs,
    IReadOnlyCollection<SyncFilePair> IdenticalFilePairs,
    IReadOnlyCollection<SyncFile> DeletedFiles
);