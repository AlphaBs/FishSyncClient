using FishSyncClient.Files;

namespace FishSyncClient;

public class StreamProgressHelper
{
    public const int DefaultBufferLength = 65536;

    public static async Task CopyToAsync(
        Stream source, 
        Stream destination,
        long bufferSize,
        IProgress<long>? progress, 
        CancellationToken cancellationToken)
    {
        if (progress == null)
        {
            await source.CopyToAsync(destination, cancellationToken);
            return;
        }

        var copyBuffer = new byte[bufferSize];
        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            int bytesRead = await source.ReadAsync(
                copyBuffer, 
                0, 
                copyBuffer.Length,
                cancellationToken)
                .ConfigureAwait(false);

            if (bytesRead == 0)
                break;

            await destination.WriteAsync(
                copyBuffer, 
                0, 
                bytesRead, 
                cancellationToken)
                .ConfigureAwait(false);
            
            progress.Report(bytesRead);
        }
    }

    public static async Task SyncFilePair(SyncFilePair pair, IProgress<ByteProgress> progress, CancellationToken cancellationToken)
    {
        var buffer = pair.Source.Metadata?.Size ?? 0;
        buffer = Math.Max(buffer, 128);
        buffer = Math.Min(buffer, DefaultBufferLength);

        var totalBytes = pair.Source.Metadata?.Size ?? 0;

        using var sourceStream = await pair.Source.OpenReadStream();
        using var targetStream = await pair.Target.OpenWriteStream();
        await CopyToAsync(
            sourceStream,
            targetStream,
            buffer,
            new SyncProgress<long>(read =>
            {
                var newTotal = pair.Source.Metadata?.Size ?? 0; // total size can be changed
                var deltaTotal = newTotal - totalBytes;
                progress.Report(new ByteProgress(deltaTotal, read));
                totalBytes = newTotal;
            }),
            cancellationToken);
    }
}