using FishSyncClient.Internals;
using FishSyncClient.Progress;

namespace FishSyncClient.Files;

public class ReadableHttpSyncFile : SyncFile
{
    private readonly HttpClient _httpClient;

    public ReadableHttpSyncFile(RootedPath path, HttpClient httpClient) : base(path)
    {
        _httpClient = httpClient;
    }

    public DateTimeOffset Uploaded { get; set; }
    public Uri? Location { get; set; }

    public override bool IsReadable => true;
    public override bool IsWritable => false;

    public override async ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(
            Location, 
            HttpCompletionOption.ResponseHeadersRead, 
            cancellationToken);

        try
        {
            response.EnsureSuccessStatusCode();

            var contentLength = response.Content.Headers.ContentLength ?? -1;
            if (contentLength >= 0)
            {
                this.Metadata ??= new();
                this.Metadata.Size = contentLength;
            }

            var stream = await response.Content.ReadAsStreamAsync();
            if (stream.CanTimeout)
                stream.ReadTimeout = 10000;
            
            return new ResponseStream(stream, response);
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException();
    }

    public override async Task CopyTo(Stream destination, IProgress<ByteProgress>? progress, CancellationToken cancellationToken)
    {
        long previousTotalBytes = Metadata?.Size ?? 0;
        using var sourceStream = await OpenReadStream(cancellationToken);
        long currentTotalBytes = Metadata?.Size ?? 0;
        progress?.Report(new ByteProgress(currentTotalBytes - previousTotalBytes, 0));

        var buffer = StreamProgressHelper.GetBufferSize(currentTotalBytes);
        await StreamProgressHelper.CopyStreamWithProgressPerBuffer(
            sourceStream,
            destination,
            buffer,
            new SyncProgress<long>(read => 
                progress?.Report(new ByteProgress(0, read))),
            cancellationToken);
    }

    private sealed class ResponseStream : Stream
    {
        private readonly Stream _inner;
        private readonly HttpResponseMessage _response;

        public ResponseStream(Stream inner, HttpResponseMessage response) =>
            (_inner, _response) = (inner, response);

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

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override long Seek(long offset, SeekOrigin origin) =>
            _inner.Seek(offset, origin);

        public override void SetLength(long value) =>
            _inner.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) =>
            _inner.Write(buffer, offset, count);

        public override Task WriteAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            _inner.WriteAsync(buffer, offset, count, cancellationToken);

        public override Task CopyToAsync(
            Stream destination,
            int bufferSize,
            CancellationToken cancellationToken) =>
            _inner.CopyToAsync(destination, bufferSize, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _inner.Dispose();
                _response.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
