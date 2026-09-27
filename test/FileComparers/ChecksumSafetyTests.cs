using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClientTest.FileComparers;

public class ChecksumSafetyTests
{
    [Theory]
    [InlineData("md5")]
    [InlineData("sha1")]
    public async Task asynchronous_checksum_matches_synchronous_checksum_across_multiple_buffers(string algorithm)
    {
        var contents = Enumerable.Range(0, 150000).Select(i => (byte)i).ToArray();
        using var first = new MemoryStream(contents);
        using var second = new MemoryStream(contents);
        Assert.Equal(ChecksumAlgorithms.ComputeHash(algorithm, first), await ChecksumAlgorithms.ComputeHashAsync(algorithm, second));
    }

    [Fact]
    public async Task metadata_checksum_comparison_ignores_hex_case()
    {
        var source = new VirtualSyncFile(RootedPath.FromSubPath("file", new PathOptions()))
        {
            Metadata = new() { Checksum = new("md5", "202CB962AC59075B964B07152D234B70") }
        };
        var target = new VirtualSyncFile(source.Path)
        {
            Metadata = new() { Checksum = new("md5", "202cb962ac59075b964b07152d234b70") }
        };
        Assert.True(await new FileChecksumMetadataComparer().AreEqual(new(source, target), default));
    }

    [Fact]
    public async Task already_cancelled_checksum_comparison_does_not_open_target()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            using var stream = new BlockingReadStream();
            var target = new TrackingLocalFile(filePath, stream);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await new LocalFileChecksumComparer().AreEqual(new(Source(), target), new CancellationToken(true)));
            Assert.False(target.Opened);
        }
        finally { File.Delete(filePath); }
    }

    [Fact]
    public async Task cancelling_inflight_checksum_interrupts_read_and_disposes_stream()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            using var cancellation = new CancellationTokenSource();
            var stream = new BlockingReadStream();
            var target = new TrackingLocalFile(filePath, stream);
            var operation = new LocalFileChecksumComparer().AreEqual(new(Source(), target), cancellation.Token).AsTask();
            await stream.ReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(cancellation.Token, target.ReceivedToken);
            Assert.Equal(cancellation.Token, stream.ReceivedToken);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.True(stream.Disposed);
        }
        finally { File.Delete(filePath); }
    }

    private static VirtualSyncFile Source() => new(RootedPath.FromSubPath("file", new PathOptions()))
    {
        Metadata = new() { Checksum = new("md5", "202cb962ac59075b964b07152d234b70") }
    };

    private sealed class TrackingLocalFile(string path, Stream stream)
        : LocalSyncFile(RootedPath.FromFullPath(System.IO.Path.GetDirectoryName(path)!, path, new PathOptions()))
    {
        public bool Opened;
        public CancellationToken ReceivedToken;
        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken)
        {
            Opened = true;
            ReceivedToken = cancellationToken;
            return new(stream);
        }
    }

    private sealed class BlockingReadStream : MemoryStream
    {
        public readonly TaskCompletionSource ReadStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken ReceivedToken;
        public bool Disposed;
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            ReadStarted.SetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
