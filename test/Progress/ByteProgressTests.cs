using FishSyncClient.Progress;

namespace FishSyncClientTest.Progress;

public class ByteProgressTests
{
    [Fact]
    public void subtracts_total_and_progressed_bytes()
    {
        var left = new ByteProgress(totalBytes: 100, progressedBytes: 80);
        var right = new ByteProgress(totalBytes: 40, progressedBytes: 15);

        var result = left - right;

        Assert.Equal(60, result.TotalBytes);
        Assert.Equal(65, result.ProgressedBytes);
    }

    [Fact]
    public void returns_zero_ratio_when_total_bytes_is_zero()
    {
        var progress = new ByteProgress(totalBytes: 0, progressedBytes: 0);

        var result = progress.GetRatio();

        Assert.Equal(0, result);
    }
}
