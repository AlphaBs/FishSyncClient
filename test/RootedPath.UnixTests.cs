using FishSyncClient;
namespace FishSyncClientTest;

[Trait("Platform", "Unix")]
public class RootedPathUnixTests
{
    [UnixOnlyTheory]
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

    [UnixOnlyTheory]
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

    [UnixOnlyFact]
    public void find_subpath_with_case_insensitive_root()
    {
        var rootedPath = RootedPath.FromFullPath(
            "/Root",
            "/root/subpath",
            new PathOptions
            {
                CaseInsensitive = true
            });

        Assert.Equal("subpath", rootedPath.SubPath);
    }

    [UnixOnlyFact]
    public void cannot_find_subpath_with_different_case_when_case_sensitive()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(
                "/Root",
                "/root/subpath",
                new PathOptions
                {
                    CaseInsensitive = false
                });
        });
    }

    [UnixOnlyTheory]
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

    [UnixOnlyTheory]
    [InlineData("dir/")]
    [InlineData(".")]
    [InlineData("././././dir")]
    [InlineData(".//////./dir")]
    public void prevent_to_parse_with_empty_root_path(string root)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(root, "/dir/file", new PathOptions());
        });
    }

    [UnixOnlyTheory]
    [InlineData(".")]
    [InlineData("dir")]
    [InlineData("dir/")]
    [InlineData("././././")]
    [InlineData("././././dir")]
    [InlineData(".//////.")]

    public void prevent_to_create_with_empty_root_path(string root)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.Create(root, "file", new PathOptions());
        });
    }

    [UnixOnlyTheory]
    [InlineData("/p1/p2", "/p1")]
    public void find_subpath_from_fullpath_and_child_root(string root, string fullPath)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(root, fullPath, new PathOptions());
        });
    }

    [UnixOnlyTheory]
    [InlineData("/pppp", "/p1")]
    [InlineData("/p1/p2/", "/p1/p2")] // /p1/p2 is a file, its root can be '/' or '/p1/'
    public void find_subpath_from_fullpath_and_unrelated(string root, string fullPath)
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.FromFullPath(root, fullPath, new PathOptions());
        });
    }

    [UnixOnlyTheory]
    [InlineData(".", "/root1/")]
    [InlineData("/.", "/root1/")]
    [InlineData("./", "/root1/")]
    [InlineData("./././a.txt", "/root1/a.txt")]
    public void relative_dot_in_subpath_is_allowed(string subpath, string expected)
    {
        var actual = RootedPath.Create("/root1", subpath, new PathOptions());
        Assert.Equal(expected, actual.GetFullPath());
    }

    [UnixOnlyFact]
    public void relative_double_dots_in_subpath_is_not_allowed()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            RootedPath.Create("/root1/root2/root3", "../../root2/root3/hello.txt", new PathOptions());
        });
    }

    [UnixOnlyTheory]
    [InlineData("a/.hidden", "/root1/a/.hidden")]
    [InlineData("a/hi.txt", "/root1/a/hi.txt")]
    [InlineData("a/file.", "/root1/a/file.")]
    [InlineData("a/dir./.file", "/root1/a/dir./.file")]
    [InlineData("a/hi../..hi", "/root1/a/hi../..hi")]
    public void file_extension_dot_in_subpath_is_allowed(string subpath, string expected)
    {
        var actual = RootedPath.Create("/root1", subpath, new PathOptions());
        Assert.Equal(expected, actual.GetFullPath());
    }
}
