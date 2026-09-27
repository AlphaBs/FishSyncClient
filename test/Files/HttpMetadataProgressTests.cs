using System.Net;
using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;

namespace FishSyncClientTest.Files;

public class HttpMetadataProgressTests
{
    [Theory]
    [InlineData(null, 3, 3)]
    [InlineData(3, 3, 3)]
    [InlineData(9, 3, 3)]
    [InlineData(1, 3, 3)]
    [InlineData(0, 3, 3)]
    [InlineData(null, 0, 0)]
    [InlineData(9, 0, 0)]
    [InlineData(9, null, 3)]
    [InlineData(null, null, 3)]
    [InlineData(0, null, 0)]
    public async Task sync_reports_response_size_without_mutating_expected_metadata(
        int? expectedSize, int? responseSize, int payloadLength)
    {
        var contents = Enumerable.Range(0, payloadLength).Select(i => (byte)i).ToArray();
        using var client = new HttpClient(new StubHandler(_ => Response(contents, responseSize)));
        var metadata = expectedSize.HasValue
            ? new SyncFileMetadata { Size = expectedSize.Value, Checksum = new("md5", "202cb962ac59075b964b07152d234b70") }
            : null;
        var source = Source(client, metadata);
        var originalHash = source.GetHashCode();
        var keys = new Dictionary<SyncFile, string> { [source] = "found" };
        var progress = new CollectingProgress<SyncFileByteProgress>();
        var target = new MemoryTarget();

        await new ParallelSyncFilePairSyncer(1).SyncFilePairs(
            [new(source, target)], null, progress, default);

        Assert.Equal(contents, target.Contents.ToArray());
        Assert.Same(metadata, source.Metadata);
        Assert.Equal(originalHash, source.GetHashCode());
        Assert.Equal("found", keys[source]);
        Assert.Equal(responseSize ?? expectedSize ?? 0, progress.Values.Sum(p => p.Progress.TotalBytes));
        Assert.Equal(payloadLength, progress.Values.Sum(p => p.Progress.ProgressedBytes));
        Assert.All(progress.Values, p => Assert.Same(source, p.SyncFile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task opening_response_keeps_metadata_and_hash_set_membership(bool omitMetadata)
    {
        using var client = new HttpClient(new StubHandler(_ => Response([1, 2, 3], 3)));
        var metadata = omitMetadata ? null : new SyncFileMetadata { Size = 99 };
        var source = Source(client, metadata);
        var keys = new HashSet<SyncFile> { source };
        var originalHash = source.GetHashCode();

        using var stream = await source.OpenReadStream();

        Assert.Same(metadata, source.Metadata);
        Assert.Equal(originalHash, source.GetHashCode());
        Assert.True(keys.TryGetValue(source, out var stored));
        Assert.Same(source, stored);
    }

    [Fact]
    public async Task size_comparer_rejects_download_that_differs_from_expected_size()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using var client = new HttpClient(new StubHandler(_ => Response([1, 2, 3], 3)));
            var source = Source(client, new() { Size = 99 });
            var target = new LocalSyncFile(RootedPath.Create(root, "file.bin", new PathOptions()));

            await Assert.ThrowsAsync<FileIntegrityException>(() =>
                new ParallelSyncFilePairSyncer(1).CompareAndSyncFilePairs(
                    [new(source, target)], new LocalFileSizeComparer(), null, null, default));

            Assert.Equal(99, source.Metadata!.Size);
            Assert.Equal(new byte[] { 1, 2, 3 }, await File.ReadAllBytesAsync(target.Path.GetFullPath()));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task repeated_downloads_use_their_own_response_size()
    {
        var requests = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            var length = ++requests == 1 ? 3 : 5;
            return Response(new byte[length], length);
        }));
        var source = Source(client, new() { Size = 10 });

        foreach (var length in new[] { 3, 5 })
        {
            using var destination = new MemoryStream();
            var progress = new CollectingProgress<ByteProgress>();
            await source.CopyTo(destination, progress, default);

            Assert.Equal(length, destination.Length);
            Assert.Equal(length - 10, progress.Values.Sum(p => p.TotalBytes));
            Assert.Equal(length, progress.Values.Sum(p => p.ProgressedBytes));
            Assert.Equal(10, source.Metadata!.Size);
        }
    }

    [Fact]
    public async Task concurrent_downloads_keep_response_sizes_separate()
    {
        var bothRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var requests = 0;
        using var client = new HttpClient(new StubHandler(_ =>
        {
            var request = Interlocked.Increment(ref requests);
            if (request == 2)
                bothRequested.SetResult();
            var length = request == 1 ? 3 : 5;
            return new(HttpStatusCode.OK)
            {
                Content = new StreamingContent(new byte[length], length, bothRequested.Task)
            };
        }));
        var source = Source(client, new() { Size = 10 });
        using var firstDestination = new MemoryStream();
        using var secondDestination = new MemoryStream();
        var firstProgress = new CollectingProgress<ByteProgress>();
        var secondProgress = new CollectingProgress<ByteProgress>();

        var first = source.CopyTo(firstDestination, firstProgress, default);
        var second = source.CopyTo(secondDestination, secondProgress, default);
        await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(3, firstDestination.Length);
        Assert.Equal(5, secondDestination.Length);
        Assert.Equal(-7, firstProgress.Values.Sum(p => p.TotalBytes));
        Assert.Equal(-5, secondProgress.Values.Sum(p => p.TotalBytes));
        Assert.Equal(3, firstProgress.Values.Sum(p => p.ProgressedBytes));
        Assert.Equal(5, secondProgress.Values.Sum(p => p.ProgressedBytes));
        Assert.Equal(10, source.Metadata!.Size);
    }

    [Fact]
    public async Task copy_preserves_open_read_stream_override_and_falls_back_to_expected_size()
    {
        using var client = new HttpClient(new StubHandler(_ => throw new InvalidOperationException("HTTP must not be called.")));
        var source = new OverriddenHttpFile(client) { Metadata = new() { Size = 3 } };
        using var destination = new MemoryStream();
        using var cancellation = new CancellationTokenSource();
        var progress = new CollectingProgress<ByteProgress>();

        await source.CopyTo(destination, progress, cancellation.Token);

        Assert.Equal(cancellation.Token, source.ReceivedToken);
        Assert.Equal(new byte[] { 1, 2, 3 }, destination.ToArray());
        Assert.Equal(0, progress.Values.Sum(p => p.TotalBytes));
        Assert.Equal(3, progress.Values.Sum(p => p.ProgressedBytes));
    }

    [Fact]
    public async Task progress_callback_failure_disposes_response_stream()
    {
        var content = new StreamingContent([1, 2, 3], 3);
        using var client = new HttpClient(new StubHandler(_ => new(HttpStatusCode.OK) { Content = content }));
        var source = Source(client, new() { Size = 9 });
        using var destination = new MemoryStream();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            source.CopyTo(destination, new ThrowingProgress(), default));

        Assert.True(content.Disposed);
        Assert.Equal(0, destination.Length);
        Assert.Equal(9, source.Metadata!.Size);
    }

    private static ReadableHttpSyncFile Source(HttpClient client, SyncFileMetadata? metadata) =>
        new(RootedPath.FromSubPath("file.bin", new PathOptions()), client)
        {
            Location = new("https://example.test/file.bin"),
            Metadata = metadata
        };

    private static HttpResponseMessage Response(byte[] contents, int? length) =>
        new(HttpStatusCode.OK) { Content = new StreamingContent(contents, length) };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class StreamingContent : HttpContent
    {
        private readonly byte[] _contents;
        private readonly Task? _ready;
        public bool Disposed { get; private set; }

        public StreamingContent(byte[] contents, int? length, Task? ready = null)
        {
            _contents = contents;
            _ready = ready;
            if (length.HasValue)
                Headers.ContentLength = length.Value;
        }

        // Content-Length 없는 응답을 재현한다.
        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) =>
            stream.WriteAsync(_contents, 0, _contents.Length);

        protected override async Task<Stream> CreateContentReadStreamAsync()
        {
            if (_ready != null)
                await _ready.WaitAsync(TimeSpan.FromSeconds(5));
            return new MemoryStream(_contents);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class MemoryTarget() : VirtualSyncFile(RootedPath.FromSubPath("target.bin", new PathOptions()))
    {
        public MemoryStream Contents { get; } = new();
        public override bool IsWritable => true;
        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default) => new(Contents);
    }

    private sealed class OverriddenHttpFile(HttpClient client)
        : ReadableHttpSyncFile(RootedPath.FromSubPath("file.bin", new PathOptions()), client)
    {
        public CancellationToken ReceivedToken { get; private set; }
        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return new(new MemoryStream([1, 2, 3]));
        }
    }

    private sealed class CollectingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = new();
        public void Report(T value) => Values.Add(value);
    }

    private sealed class ThrowingProgress : IProgress<ByteProgress>
    {
        public void Report(ByteProgress value) => throw new InvalidOperationException("Progress failed.");
    }
}
