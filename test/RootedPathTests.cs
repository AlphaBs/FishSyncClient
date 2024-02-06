using FishSyncClient;

namespace FishSyncClientTest;

public class RootedPathTests
{
    [Theory]
    [InlineData("/", "/", "/")]
    [InlineData("/", "subpath", "/subpath")]
    [InlineData("/root", "subpath", "/root/subpath")]
    [InlineData("/root/", "subpath", "/root/subpath")]
    [InlineData("/root/", "/subpath", "/root/subpath")]
    [InlineData("/root/", "subpath/", "/root/subpath/")]
    [InlineData("/root/", "/subpath/", "/root/subpath/")]
    [InlineData("//root///", "//subpath//", "/root/subpath/")]
    [InlineData("//root\\//\\", "//with\\alt\\path//\\//", "/root/with/alt/path/")]
    public void normalize_fullpath(string root, string subpath, string expectedFullPath)
    {
        var rootedPath = RootedPath.Create(root, subpath, new PathOptions());
        var actualFullPath = rootedPath.GetFullPath();
        Assert.Equal(expectedFullPath, actualFullPath);
    }

    [Theory]
    [InlineData("", "/root/subpath", "root/subpath")]
    [InlineData("/", "/root/subpath", "root/subpath")]
    [InlineData("/root", "/root/subpath", "subpath")]
    [InlineData("/root/", "/root/subpath", "subpath")]
    [InlineData("/root/subpath", "/root/subpath/", "")]
    [InlineData("/root/subpath/", "/root/subpath/", "")]
    public void find_subpath_from_fullpath_and_root(string root, string fullPath, string expectedSubPath)
    {
        var rootedPath = RootedPath.FromFullPath(root, fullPath, new PathOptions());
        Assert.Equal(expectedSubPath, rootedPath.SubPath);
    }

    [Theory]
    [InlineData("/", "")]
    [InlineData("subpath", "subpath")]
    [InlineData("/subpath", "subpath")]
    [InlineData("subpath/", "subpath/")]
    [InlineData("/subpath/", "subpath/")]
    public void create_empty_rooted_path(string subpath, string expectedSubPath)
    {
        var rootedPath = RootedPath.FromSubPath(subpath, new PathOptions());
        Assert.Equal("", rootedPath.Root);
        Assert.Equal(expectedSubPath, rootedPath.SubPath);
    }

    [Theory]
    [InlineData("/p1/p2", "/p1")]
    public void find_subpath_from_fullpath_and_child_root(string root, string fullPath)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(root, fullPath, new PathOptions());
        });
    }

    [Theory]
    [InlineData("/pppp", "/p1")]
    [InlineData("/p1/p2/", "/p1/p2")] // /p1/p2 is a file, its root can be '/' or '/p1/'
    public void find_subpath_from_fullpath_and_unrelated(string root, string fullPath)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(root, fullPath, new PathOptions());
        });
    }
}