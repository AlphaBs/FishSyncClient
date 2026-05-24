using FishSyncClient;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClientTest.Progress;

public class SyncFileProgressCollectionTests
{
    [Fact]
    public void clear_progress_resets_aggregate_and_items()
    {
        var file = new VirtualSyncFile(RootedPath.FromSubPath("file.txt", new PathOptions()));
        var sut = new SyncFileProgressCollection(new[] { file });

        sut.AddProgress(file, new ByteProgress(totalBytes: 100, progressedBytes: 40));
        sut.ClearProgress();

        var aggregate = sut.AggregateProgress();
        var item = sut.FindItemByPath("file.txt");
        Assert.Equal(0, aggregate.TotalBytes);
        Assert.Equal(0, aggregate.ProgressedBytes);
        Assert.NotNull(item);
        Assert.Equal(0, item!.Progress.TotalBytes);
        Assert.Equal(0, item.Progress.ProgressedBytes);
    }

    [Fact]
    public void add_progress_returns_false_for_unknown_path()
    {
        var sut = new SyncFileProgressCollection();

        var result = sut.AddProgress("missing.txt", new ByteProgress(totalBytes: 100, progressedBytes: 40));

        Assert.False(result);
        var aggregate = sut.AggregateProgress();
        Assert.Equal(0, aggregate.TotalBytes);
        Assert.Equal(0, aggregate.ProgressedBytes);
    }
}
