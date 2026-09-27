using FishSyncClient;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;

namespace FishSyncClientTest;

public class ParallelFailureTests
{
    [Theory]
    [InlineData("enumeration")]
    [InlineData("progress")]
    [InlineData("cancellation")]
    public async Task failure_does_not_return_until_inflight_copy_finishes(string failure)
    {
        using var cancellation = new CancellationTokenSource();
        var source = new GatedSource();
        var pair = new SyncFilePair(source, new MemoryTarget());
        var triggered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        IEnumerable<SyncFilePair> Pairs()
        {
            yield return pair;
            Assert.True(source.Started.Wait(TimeSpan.FromSeconds(5)));
            if (failure == "enumeration")
            {
                triggered.SetResult();
                throw new InvalidOperationException("enumeration failed");
            }
            if (failure == "cancellation")
            {
                cancellation.Cancel();
                triggered.SetResult();
            }
            yield return pair;
        }

        var progress = new DirectProgress<FileProgressEvent>(value =>
        {
            if (failure == "progress" && value.EventType == FileProgressEventType.Queue && value.TotalFiles == 2)
            {
                triggered.SetResult();
                throw new InvalidOperationException("progress failed");
            }
        });
        var operation = Task.Run(() => new ParallelSyncFilePairSyncer(1).SyncFilePairs(Pairs(), progress, null, cancellation.Token));
        try
        {
            await triggered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.NotSame(operation, await Task.WhenAny(operation, Task.Delay(100)));
        }
        finally
        {
            source.Release.TrySetResult();
        }

        if (failure == "cancellation")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
        else
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(failure + " failed", exception.Message);
        }
        Assert.Equal(1, source.CompletedWrites);
    }

    private sealed class GatedSource() : SyncFile(RootedPath.FromSubPath("file", new PathOptions()))
    {
        public readonly ManualResetEventSlim Started = new(false);
        public readonly TaskCompletionSource Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int CompletedWrites;
        public override bool IsReadable => true;
        public override bool IsWritable => false;
        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override async Task CopyTo(Stream destination, IProgress<ByteProgress>? progress, CancellationToken cancellationToken)
        {
            Started.Set();
            await Release.Task;
            // A custom source may not cooperate with cancellation; the caller must still await it.
            await destination.WriteAsync(new byte[] { 1, 2, 3 });
            Interlocked.Increment(ref CompletedWrites);
        }
    }

    private sealed class MemoryTarget() : SyncFile(RootedPath.FromSubPath("file", new PathOptions()))
    {
        public override bool IsReadable => false;
        public override bool IsWritable => true;
        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default) => new(new MemoryStream());
        public override Task CopyTo(Stream destination, IProgress<ByteProgress>? progress, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class DirectProgress<T>(Action<T> callback) : IProgress<T>
    {
        public void Report(T value) => callback(value);
    }
}
