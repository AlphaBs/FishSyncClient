using FishSyncClient;

namespace FishSyncClientTest;

public class RootedPathTests
{
    [Fact]
    public void equals_returns_false_for_null()
    {
        var path = RootedPath.FromSubPath("file.txt", new PathOptions());

        Assert.False(path.Equals(null));
    }

    [Fact]
    public void equals_returns_false_for_different_type_with_same_string()
    {
        var path = RootedPath.FromSubPath("file.txt", new PathOptions());

        Assert.False(path.Equals("file.txt"));
    }

    [Fact]
    public void equals_returns_true_for_same_path()
    {
        var path1 = RootedPath.FromSubPath("file.txt", new PathOptions());
        var path2 = RootedPath.FromSubPath("file.txt", new PathOptions());

        Assert.True(path1.Equals(path2));
    }
}
