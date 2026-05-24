using FishSyncClient;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClientTest.Files;

public class LocalSyncFileTests
{
    [Fact]
    public async Task copy_to_reports_final_progress_delta()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            var contents = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray();
            await File.WriteAllBytesAsync(Path.Combine(root, "source.bin"), contents);

            var file = new LocalSyncFile(RootedPath.Create(root, "source.bin", new PathOptions()));
            var progress = new CollectingProgress<ByteProgress>();

            using var destination = new MemoryStream();
            await file.CopyTo(destination, progress, CancellationToken.None);

            Assert.Equal(contents.Length, progress.Values.Sum(p => p.ProgressedBytes));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class CollectingProgress<T> : IProgress<T>
    {
        public List<T> Values { get; } = new();

        public void Report(T value) => Values.Add(value);
    }
}
