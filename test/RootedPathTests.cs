using FishSyncClient;

namespace FishSyncClientTest;

public class RootedPathTests
{
    [Theory]
    [InlineData("/", "/", "/", "", "/")]
    [InlineData("/a", "/", "/a/", "", "/a/")]
    [InlineData("/", "subpath", "/", "subpath", "/subpath")]
    [InlineData("/root", "subpath", "/root/", "subpath", "/root/subpath")]
    [InlineData("/root/", "subpath", "/root/", "subpath", "/root/subpath")]
    [InlineData("/root/", "/subpath", "/root/", "subpath", "/root/subpath")]
    [InlineData("/root/", "subpath/", "/root/", "subpath/", "/root/subpath/")]
    [InlineData("/root/", "/subpath/", "/root/", "subpath/", "/root/subpath/")]
    [InlineData("//root///", "//subpath//", "/root/", "subpath/", "/root/subpath/")]
    [InlineData("//root\\//\\", "//with\\alt\\path//\\//", "/root/", "with/alt/path/", "/root/with/alt/path/")]
    public void normalize_fullpath(string root, string subpath, string expectedRoot, string expectedSubpath, string expectedFullPath)
    {
        var rootedPath = RootedPath.Create(root, subpath, new PathOptions());
        Assert.Equal(expectedRoot, rootedPath.Root);
        Assert.Equal(expectedSubpath, rootedPath.SubPath);
        Assert.Equal(expectedFullPath, rootedPath.GetFullPath());
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

    [Theory]
    [InlineData(".", "/root1/")]
    [InlineData("/.", "/root1/")]
    [InlineData("./", "/root1/")]
    [InlineData("./././a.txt", "/root1/a.txt")]
    public void relative_dot_in_subpath_is_allowed(string subpath, string expected)
    {
        var actual = RootedPath.Create("/root1", subpath, new PathOptions());
        Assert.Equal(expected, actual.GetFullPath());
    }

    [Fact]
    public void relative_double_dots_in_subpath_is_not_allowed()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.Create("/root1/root2/root3", "../../root2/root3/hello.txt", new PathOptions());
        });
    }

    [Theory]
    [InlineData("a/.hidden", "/root1/a/.hidden")]
    [InlineData("a/hi.txt", "/root1/a/hi.txt")]
    [InlineData("a/file.", "/root1/a/file.")]
    [InlineData("a/dir./.file", "/root1/a/dir./.file")]
    public void file_extension_dot_in_subpath_is_allowed(string subpath, string expected)
    {
        var actual = RootedPath.Create("/root1", subpath, new PathOptions());
        Assert.Equal(expected, actual.GetFullPath());
    }
}