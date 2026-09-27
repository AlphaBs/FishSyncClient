using FishSyncClient;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClientTest.Files;

public class LocalCopyProgressTests
{
    [Fact]
    public async Task progress_includes_bytes_copied_while_callback_runs()
    {
        var stream = new CoordinatedStream();
        var file = new StreamBackedLocalFile(stream);
        long reported = 0;
        var progress = new CallbackProgress(delta =>
        {
            reported += delta.ProgressedBytes;
            stream.Position = 20;
            stream.Finish.TrySetResult();
        });
        using var destination = new MemoryStream();

        await file.CopyTo(destination, progress, default);

        Assert.Equal(20, destination.Length);
        Assert.Equal(20, reported);
    }

    [Fact]
    public async Task failing_progress_callback_still_waits_for_copy_to_finish()
    {
        var stream = new CoordinatedStream();
        var file = new StreamBackedLocalFile(stream);
        using var destination = new MemoryStream();
        var operation = file.CopyTo(destination, new CallbackProgress(_ => throw new InvalidOperationException("progress")), default);
        Assert.False(operation.IsCompleted);
        Assert.False(stream.Disposed);
        stream.Finish.SetResult();

        await Assert.ThrowsAsync<InvalidOperationException>(() => operation);
        Assert.True(stream.Disposed);
    }

    private sealed class CallbackProgress(Action<ByteProgress> report) : IProgress<ByteProgress>
    {
        public void Report(ByteProgress value) => report(value);
    }

    private sealed class StreamBackedLocalFile(Stream stream)
        : LocalSyncFile(RootedPath.Create(System.IO.Path.GetTempPath(), "progress-source", new PathOptions()))
    {
        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken) => new(stream);
    }

    private sealed class CoordinatedStream : MemoryStream
    {
        public readonly TaskCompletionSource Finish = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed;
        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
        {
            Position = 10;
            destination.Write(new byte[20]);
            return Finish.Task;
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
