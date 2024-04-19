namespace FishSyncClient.Internals;

internal class StreamProgressHelper
{
    public const int DefaultBufferLength = 65536;

    public static async Task CopyStreamWithProgressPerBuffer(
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

    public static async Task CopyStreamWithPeriodicProgress(
        Stream source,
        Stream target,
        int interval,
        IProgress<long> progress,
        CancellationToken cancellationToken)
    {
        long initPosition = source.Position;
        var copyTask = source.CopyToAsync(target, cancellationToken);
        await MonitorStreamPosition(copyTask, source, initPosition, interval, progress);
    }

    public static async Task MonitorStreamPosition(
        Task task, 
        Stream stream, 
        long initPosition,
        int interval, 
        IProgress<long> delta)
    {
        long previousPosition = initPosition;
        while (!task.IsCompleted)
        {
            delta?.Report(stream.Position - previousPosition);
            previousPosition = stream.Position;
            await Task.WhenAny(task, Task.Delay(interval));
        }
        await task;
    }

    public static long GetBufferSize(long size)
    {
        var buffer = Math.Min(size, DefaultBufferLength);
        return Math.Max(buffer, 128);
    }
}