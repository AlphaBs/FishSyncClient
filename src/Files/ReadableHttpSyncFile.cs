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

        var contentLength = response.Content.Headers.ContentLength ?? -1;
        if (contentLength > 0)
        {
            this.Metadata ??= new(); 
            this.Metadata.Size = contentLength;
        }

        var stream = await response.Content.ReadAsStreamAsync();
        if (stream.CanTimeout)
            stream.ReadTimeout = 10000;
            
        return stream;
    }

    public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException("cannot write");
    }

    public override async Task CopyTo(Stream destination, IProgress<ByteProgress>? progress, CancellationToken cancellationToken)
    {
        using var sourceStream = await OpenReadStream(cancellationToken);
        long totalBytes = Metadata?.Size ?? 0;
        progress?.Report(new ByteProgress(totalBytes, 0));

        var buffer = StreamProgressHelper.GetBufferSize(totalBytes);
        await StreamProgressHelper.CopyStreamWithProgressPerBuffer(
            sourceStream,
            destination,
            buffer,
            new SyncProgress<long>(read => 
                progress?.Report(new ByteProgress(0, read))),
            cancellationToken);
    }
}