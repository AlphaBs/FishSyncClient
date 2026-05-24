namespace FishSyncClient.Gui;

/// <summary>
/// An <see cref="IProgress{T}"/> that invokes its callback synchronously on the reporting thread.
/// Unlike <see cref="System.Progress{T}"/>, it does NOT marshal every report to the captured
/// <see cref="SynchronizationContext"/>. This avoids flooding the UI thread message queue when
/// progress is reported very frequently (e.g. once per stream buffer). The callback must therefore
/// be thread-safe and cheap.
/// </summary>
public sealed class SynchronousProgress<T> : IProgress<T>
{
    private readonly Action<T> _handler;

    public SynchronousProgress(Action<T> handler) => _handler = handler;

    public void Report(T value) => _handler(value);
}
