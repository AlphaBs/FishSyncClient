using System.Net;
using FishSyncClient;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClientTest.Files;

public class ReadableHttpSyncFileTests
{
    [Fact]
    public async Task disposes_http_response_when_returned_stream_is_disposed()
    {
        var contentStream = new TrackingStream(new MemoryStream(new byte[] { 1, 2, 3 }));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(contentStream)
        });
        var httpClient = new HttpClient(handler);
        var file = new ReadableHttpSyncFile(
            RootedPath.FromSubPath("file.bin", new PathOptions()),
            httpClient)
        {
            Location = new Uri("https://example.test/file.bin")
        };

        var stream = await file.OpenReadStream();
        stream.Dispose();

        Assert.True(contentStream.IsDisposed);
    }

    [Fact]
    public async Task open_read_stream_sets_zero_length_metadata()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(Array.Empty<byte>())
        });
        var httpClient = new HttpClient(handler);
        var file = new ReadableHttpSyncFile(
            RootedPath.FromSubPath("empty.bin", new PathOptions()),
            httpClient)
        {
            Location = new Uri("https://example.test/empty.bin")
        };

        using var stream = await file.OpenReadStream();

        Assert.NotNull(file.Metadata);
        Assert.Equal(0, file.Metadata!.Size);
    }

    [Fact]
    public async Task returned_stream_forwards_read_async_to_http_content_stream()
    {
        var contentStream = new TrackingStream(new MemoryStream(new byte[] { 1, 2, 3 }));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(contentStream)
        });
        var httpClient = new HttpClient(handler);
        var file = new ReadableHttpSyncFile(
            RootedPath.FromSubPath("file.bin", new PathOptions()),
            httpClient)
        {
            Location = new Uri("https://example.test/file.bin")
        };

        using var stream = await file.OpenReadStream();
        var buffer = new byte[1];
        var read = await stream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None);

        Assert.Equal(1, read);
        Assert.Equal(1, buffer[0]);
        Assert.Equal(1, contentStream.ReadAsyncCallCount);
    }

    [Fact]
    public async Task returned_stream_forwards_copy_to_async_to_http_content_stream()
    {
        var contentStream = new TrackingStream(new MemoryStream(new byte[] { 1, 2, 3 }));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(contentStream)
        });
        var httpClient = new HttpClient(handler);
        var file = new ReadableHttpSyncFile(
            RootedPath.FromSubPath("file.bin", new PathOptions()),
            httpClient)
        {
            Location = new Uri("https://example.test/file.bin")
        };

        using var stream = await file.OpenReadStream();
        using var destination = new MemoryStream();
        await stream.CopyToAsync(destination, 81920, CancellationToken.None);

        Assert.Equal(1, contentStream.CopyToAsyncCallCount);
        Assert.Equal(new byte[] { 1, 2, 3 }, destination.ToArray());
    }

    [Fact]
    public async Task disposes_http_response_when_status_code_is_not_success()
    {
        var contentStream = new TrackingStream(new MemoryStream(new byte[] { 1, 2, 3 }));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StreamContent(contentStream)
        });
        var httpClient = new HttpClient(handler);
        var file = new ReadableHttpSyncFile(
            RootedPath.FromSubPath("missing.bin", new PathOptions()),
            httpClient)
        {
            Location = new Uri("https://example.test/missing.bin")
        };

        await Assert.ThrowsAsync<HttpRequestException>(async () => await file.OpenReadStream());

        Assert.True(contentStream.IsDisposed);
    }

    [Fact]
    public async Task copy_to_throws_when_cancellation_is_requested_during_copy()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var contentStream = new NonSeekableStream(new MemoryStream(new byte[1024]));
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(contentStream)
        });
        var httpClient = new HttpClient(handler);
        var file = new ReadableHttpSyncFile(
            RootedPath.FromSubPath("file.bin", new PathOptions()),
            httpClient)
        {
            Location = new Uri("https://example.test/file.bin")
        };
        using var destination = new CancelAfterFirstWriteStream(cancellationTokenSource);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await file.CopyTo(
                destination,
                new CollectingProgress<ByteProgress>(),
                cancellationTokenSource.Token));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_responseFactory(request));
        }
    }

    private sealed class TrackingStream : Stream
    {
        private readonly Stream _inner;

        public TrackingStream(Stream inner)
        {
            _inner = inner;
        }

        public bool IsDisposed { get; private set; }
        public int ReadAsyncCallCount { get; private set; }
        public int CopyToAsyncCallCount { get; private set; }
        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => _inner.CanSeek;
        public override bool CanWrite => _inner.CanWrite;
        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            ReadAsyncCallCount++;
            return _inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override Task CopyToAsync(
            Stream destination,
            int bufferSize,
            CancellationToken cancellationToken)
        {
            CopyToAsyncCallCount++;
            return _inner.CopyToAsync(destination, bufferSize, cancellationToken);
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            _inner.Seek(offset, origin);

        public override void SetLength(long value) =>
            _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                IsDisposed = true;
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    private sealed class NonSeekableStream : Stream
    {
        private readonly Stream _inner;

        public NonSeekableStream(Stream inner)
        {
            _inner = inner;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, Math.Min(count, 64));

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, Math.Min(count, 64), cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _inner.Dispose();

            base.Dispose(disposing);
        }
    }

    private sealed class CancelAfterFirstWriteStream : MemoryStream
    {
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _hasCancelled;

        public CancelAfterFirstWriteStream(CancellationTokenSource cancellationTokenSource)
        {
            _cancellationTokenSource = cancellationTokenSource;
        }

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            var task = base.WriteAsync(buffer, offset, count, cancellationToken);
            if (!_hasCancelled)
            {
                _hasCancelled = true;
                _cancellationTokenSource.Cancel();
            }

            return task;
        }
    }

    private sealed class CollectingProgress<T> : IProgress<T>
    {
        public void Report(T value)
        {
        }
    }
}
