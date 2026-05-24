using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;

namespace FishSyncClientTest;

public class ParallelSyncFilePairSyncerTests
{
    [Fact]
    public void constructor_throws_when_parallelism_is_less_than_one()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParallelSyncFilePairSyncer(0));
    }

    [Fact]
    public async Task compare_file_pairs_reports_done_compare()
    {
        var sut = new ParallelSyncFilePairSyncer(1);
        var progress = new CollectingProgress<FileProgressEvent>();

        await sut.CompareFilePairs(
            CreatePairs(1),
            new DelayComparer(TimeSpan.Zero),
            progress,
            byteProgress: null,
            cancellationToken: default);

        Assert.Contains(progress.Values, e => e.EventType == FileProgressEventType.DoneCompare);
        Assert.DoesNotContain(progress.Values, e => e.EventType == FileProgressEventType.DoneSync);
    }

    [Fact]
    public async Task compare_file_pairs_uses_configured_parallelism()
    {
        var sut = new ParallelSyncFilePairSyncer(2);
        var comparer = new DelayComparer(TimeSpan.FromMilliseconds(100));

        await sut.CompareFilePairs(
            CreatePairs(4),
            comparer,
            fileProgress: null,
            byteProgress: null,
            cancellationToken: default);

        Assert.Equal(2, comparer.MaxObservedConcurrency);
    }

    [Fact]
    public async Task sync_file_pairs_uses_configured_parallelism()
    {
        var sut = new ParallelSyncFilePairSyncer(2);
        var counter = new ConcurrencyCounter();
        var pairs = Enumerable.Range(0, 4)
            .Select(i => new SyncFilePair(
                new DelayCopySyncFile(RootedPath.FromSubPath($"source-{i}", new PathOptions()), counter),
                new MemoryTargetSyncFile(RootedPath.FromSubPath($"target-{i}", new PathOptions()))))
            .ToArray();

        await sut.SyncFilePairs(
            pairs,
            fileProgress: null,
            byteProgress: null,
            cancellationToken: default);

        Assert.Equal(2, counter.MaxObservedConcurrency);
    }

    [Fact]
    public async Task sync_file_pairs_reports_start_and_done_sync()
    {
        var sut = new ParallelSyncFilePairSyncer(1);
        var progress = new CollectingProgress<FileProgressEvent>();
        var pair = new SyncFilePair(
            new CopyTrackingSyncFile(RootedPath.FromSubPath("source", new PathOptions())),
            new MemoryTargetSyncFile(RootedPath.FromSubPath("target", new PathOptions())));

        await sut.SyncFilePairs(
            new[] { pair },
            progress,
            byteProgress: null,
            cancellationToken: default);

        Assert.Contains(progress.Values, e => e.EventType == FileProgressEventType.StartSync);
        Assert.Contains(progress.Values, e => e.EventType == FileProgressEventType.DoneSync);
        Assert.DoesNotContain(progress.Values, e => e.EventType == FileProgressEventType.DoneCompare);
    }

    [Fact]
    public async Task compare_and_sync_file_pairs_updates_file_when_compare_fails_then_passes_after_sync()
    {
        var sut = new ParallelSyncFilePairSyncer(1);
        var source = new CopyTrackingSyncFile(RootedPath.FromSubPath("source", new PathOptions()));
        var pair = new SyncFilePair(
            source,
            new MemoryTargetSyncFile(RootedPath.FromSubPath("target", new PathOptions())));

        var result = await sut.CompareAndSyncFilePairs(
            new[] { pair },
            new SequenceComparer(false, true),
            fileProgress: null,
            byteProgress: null,
            cancellationToken: default);

        Assert.Equal(1, source.CopyCount);
        Assert.Single(result.UpdatedFiles);
        Assert.Empty(result.IdenticalFiles);
    }

    [Fact]
    public async Task compare_and_sync_file_pairs_throws_when_file_integrity_check_fails_after_sync()
    {
        var sut = new ParallelSyncFilePairSyncer(1);
        var source = new CopyTrackingSyncFile(RootedPath.FromSubPath("source", new PathOptions()));
        var pair = new SyncFilePair(
            source,
            new MemoryTargetSyncFile(RootedPath.FromSubPath("target", new PathOptions())));

        await Assert.ThrowsAsync<FileIntegrityException>(async () =>
            await sut.CompareAndSyncFilePairs(
                new[] { pair },
                new SequenceComparer(false, false),
                fileProgress: null,
                byteProgress: null,
                cancellationToken: default));

        Assert.Equal(1, source.CopyCount);
    }

    [Fact]
    public async Task compare_and_sync_file_pairs_does_not_copy_identical_file_and_reports_completed_bytes()
    {
        var sut = new ParallelSyncFilePairSyncer(1);
        var source = new CopyTrackingSyncFile(
            RootedPath.FromSubPath("source", new PathOptions()),
            size: 123);
        var pair = new SyncFilePair(
            source,
            new MemoryTargetSyncFile(RootedPath.FromSubPath("target", new PathOptions())));
        var byteProgress = new CollectingProgress<SyncFileByteProgress>();

        var result = await sut.CompareAndSyncFilePairs(
            new[] { pair },
            new SequenceComparer(true),
            fileProgress: null,
            byteProgress,
            cancellationToken: default);

        Assert.Equal(0, source.CopyCount);
        Assert.Empty(result.UpdatedFiles);
        Assert.Single(result.IdenticalFiles);
        Assert.Contains(byteProgress.Values, p =>
            p.Progress.TotalBytes == 0 &&
            p.Progress.ProgressedBytes == 123);
    }

    [Fact]
    public async Task compare_file_pairs_observes_cancellation()
    {
        var sut = new ParallelSyncFilePairSyncer(1);
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await sut.CompareFilePairs(
                CreatePairs(1),
                new DelayComparer(TimeSpan.Zero),
                fileProgress: null,
                byteProgress: null,
                cancellationToken: cancellationTokenSource.Token));
    }

    private static SyncFilePair[] CreatePairs(int count)
    {
        return Enumerable.Range(0, count)
            .Select(i => new SyncFilePair(
                new VirtualSyncFile(RootedPath.FromSubPath($"source-{i}", new PathOptions())),
                new VirtualSyncFile(RootedPath.FromSubPath($"target-{i}", new PathOptions()))))
            .ToArray();
    }

    private sealed class CollectingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = new();

        public void Report(T value) => Values.Add(value);
    }

    private sealed class DelayComparer : IFileComparer
    {
        private readonly TimeSpan _delay;
        private readonly ConcurrencyCounter _counter = new();

        public DelayComparer(TimeSpan delay)
        {
            _delay = delay;
        }

        public int MaxObservedConcurrency => _counter.MaxObservedConcurrency;

        public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
        {
            await _counter.Run(_delay, cancellationToken);
            return true;
        }
    }

    private sealed class DelayCopySyncFile : SyncFile
    {
        private readonly ConcurrencyCounter _counter;

        public DelayCopySyncFile(RootedPath path, ConcurrencyCounter counter) : base(path)
        {
            _counter = counter;
        }

        public override bool IsReadable => true;
        public override bool IsWritable => false;

        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override Task CopyTo(
            Stream destination,
            IProgress<ByteProgress>? progress,
            CancellationToken cancellationToken)
        {
            return _counter.Run(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private sealed class CopyTrackingSyncFile : SyncFile
    {
        public CopyTrackingSyncFile(RootedPath path, long size = 0) : base(path)
        {
            Metadata = new SyncFileMetadata
            {
                Size = size
            };
        }

        public int CopyCount { get; private set; }
        public override bool IsReadable => true;
        public override bool IsWritable => false;

        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override Task CopyTo(
            Stream destination,
            IProgress<ByteProgress>? progress,
            CancellationToken cancellationToken)
        {
            CopyCount++;
            if (Metadata?.Size > 0)
                progress?.Report(new ByteProgress(0, Metadata.Size));

            return Task.CompletedTask;
        }
    }

    private sealed class MemoryTargetSyncFile : SyncFile
    {
        public MemoryTargetSyncFile(RootedPath path) : base(path)
        {
        }

        public override bool IsReadable => false;
        public override bool IsWritable => true;

        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default) =>
            new(new MemoryStream());

        public override Task CopyTo(
            Stream destination,
            IProgress<ByteProgress>? progress,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class ConcurrencyCounter
    {
        private int _currentConcurrency;
        private int _maxObservedConcurrency;

        public int MaxObservedConcurrency => _maxObservedConcurrency;

        public async Task Run(TimeSpan delay, CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref _currentConcurrency);
            updateMax(current);
            try
            {
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }

        private void updateMax(int current)
        {
            while (true)
            {
                var max = _maxObservedConcurrency;
                if (current <= max)
                    return;

                if (Interlocked.CompareExchange(ref _maxObservedConcurrency, current, max) == max)
                    return;
            }
        }
    }

    private sealed class SequenceComparer : IFileComparer
    {
        private readonly Queue<bool> _results;

        public SequenceComparer(params bool[] results)
        {
            _results = new Queue<bool>(results);
        }

        public ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
        {
            if (_results.Count == 0)
                return new ValueTask<bool>(true);

            return new ValueTask<bool>(_results.Dequeue());
        }
    }
}
