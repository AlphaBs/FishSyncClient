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

            var contentLength = response.Content.Headers.ContentLength;

            var stream = await response.Content.ReadAsStreamAsync();
            if (stream.CanTimeout)
                stream.ReadTimeout = 10000;
            
            return new ResponseStream(stream, response, contentLength);
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
        var registeredSize = Metadata?.Size ?? 0;
        using var sourceStream = await OpenReadStream(cancellationToken);
        // 응답 크기는 진행률만 보정한다. 기대 메타데이터와 파일의 해시는 유지한다.
        // 재정의된 OpenReadStream이 일반 스트림을 반환하면 등록된 크기를 사용한다.
        var transferSize = sourceStream is ResponseStream responseStream
            ? responseStream.ContentLength ?? registeredSize
            : registeredSize;
        progress?.Report(new ByteProgress(transferSize - registeredSize, 0));

        var buffer = StreamProgressHelper.GetBufferSize(transferSize);
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

        public ResponseStream(Stream inner, HttpResponseMessage response, long? contentLength) =>
            (_inner, _response, ContentLength) = (inner, response, contentLength);

        public long? ContentLength { get; }

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
