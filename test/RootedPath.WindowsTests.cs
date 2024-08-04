using System.Runtime.InteropServices;
using FishSyncClient;

namespace FishSyncClientTest;

[Trait("Platform", "Windows")]
public class RootedPathWindowsTests
{
    public RootedPathWindowsTests()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            throw new PlatformNotSupportedException("NOT WINDOWS");
    }

    [Theory]
    [InlineData("C:/", "/", "C:/", "", "C:/")]
    [InlineData("C:/a", "/", "C:/a/", "", "C:/a/")]
    [InlineData("C:/", "subpath", "C:/", "subpath", "C:/subpath")]
    [InlineData("C:/root", "subpath", "C:/root/", "subpath", "C:/root/subpath")]
    [InlineData("C:/root/", "subpath", "C:/root/", "subpath", "C:/root/subpath")]
    [InlineData("C:/root/", "/subpath", "C:/root/", "subpath", "C:/root/subpath")]
    [InlineData("C:/root/", "subpath/", "C:/root/", "subpath/", "C:/root/subpath/")]
    [InlineData("C:/root/", "/subpath/", "C:/root/", "subpath/", "C:/root/subpath/")]
    [InlineData("C://root///", "//subpath//", "C:/root/", "subpath/", "C:/root/subpath/")]
    [InlineData("C://root\\//\\", "//with\\alt\\path//\\//", "C:/root/", "with/alt/path/", "C:/root/with/alt/path/")]
    public void normalize_fullpath(string root, string subpath, string expectedRoot, string expectedSubpath, string expectedFullPath)
    {
        var rootedPath = RootedPath.Create(root, subpath, new PathOptions());
        Assert.Equal(expectedRoot, rootedPath.Root);
        Assert.Equal(expectedSubpath, rootedPath.SubPath);
        Assert.Equal(expectedFullPath, rootedPath.GetFullPath());
    }

    [Theory]
    [InlineData("C:/", "C:/root/subpath", "root/subpath")]
    [InlineData("C:/root", "C:/root/subpath", "subpath")]
    [InlineData("C:/root/", "C:/root/subpath", "subpath")]
    [InlineData("C:/root/subpath", "C:/root/subpath/", "")]
    [InlineData("C:/root/subpath/", "C:/root/subpath/", "")]
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
    [InlineData("dir/")]
    [InlineData(".")]
    [InlineData("././././dir")]
    [InlineData(".//////./dir")]
    public void prevent_to_parse_with_empty_root_path(string root)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(root, "C:/dir/file", new PathOptions());
        });
    }

    [Theory]
    [InlineData(".")]
    [InlineData("dir")]
    [InlineData("dir/")]
    [InlineData("././././")]
    [InlineData("././././dir")]
    [InlineData(".//////.")]
    [InlineData("/dir")] // Windows ������ / ���� �����ϴ� ��δ� �ùٸ� ��ΰ� �ƴ�
    public void prevent_to_create_with_relative_root_path(string root)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.Create(root, "file", new PathOptions());
        });
    }

    [Theory]
    [InlineData("C:/p1/p2", "C:/p1")]
    public void find_subpath_from_fullpath_and_child_root(string root, string fullPath)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(root, fullPath, new PathOptions());
        });
    }

    [Theory]
    [InlineData("C:/pppp", "C:/p1")]
    [InlineData("C:/p1/p2/", "C:/p1/p2")] // /p1/p2 is a file, its root can be '/' or '/p1/'
    public void find_subpath_from_fullpath_and_unrelated(string root, string fullPath)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(root, fullPath, new PathOptions());
        });
    }

    [Theory]
    [InlineData(".", "C:/root1/")]
    [InlineData("/.", "C:/root1/")]
    [InlineData("./", "C:/root1/")]
    [InlineData("./././a.txt", "C:/root1/a.txt")]
    public void relative_dot_in_subpath_is_allowed(string subpath, string expected)
    {
        var actual = RootedPath.Create("C:/root1", subpath, new PathOptions());
        Assert.Equal(expected, actual.GetFullPath());
    }

    [Fact]
    public void relative_double_dots_in_subpath_is_not_allowed()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.Create("C:/root1/root2/root3", "../../root2/root3/hello.txt", new PathOptions());
        });
    }

    [Theory]
    [InlineData("a/.hidden", "C:/root1/a/.hidden")]
    [InlineData("a/hi.txt", "C:/root1/a/hi.txt")]
    [InlineData("a/file.", "C:/root1/a/file.")]
    [InlineData("a/dir./.file", "C:/root1/a/dir./.file")]
    [InlineData("a/hi../..hi", "C:/root1/a/hi../..hi")]
    public void file_extension_dot_in_subpath_is_allowed(string subpath, string expected)
    {
        var actual = RootedPath.Create("C:/root1", subpath, new PathOptions());
        Assert.Equal(expected, actual.GetFullPath());
    }
}