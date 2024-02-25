using FishSyncClient;

namespace FishSyncClientTest;

public class FishFileTestBase
{
    public PathOptions PathOptions = new PathOptions
    {
        PathSeperator = '/',
        AltPathSeperator = '\\',
        CaseInsensitivePath = false
    };

    public SyncFile[] CreateSourcePaths(params string[] paths)
    {
        return CreatePathsFromRoot("source", paths);
    }

    public SyncFile[] CreateTargetPaths(params string[] paths)
    {
        return CreatePathsFromRoot("target", paths);
    }

    public SyncFile[] CreatePathsFromRoot(string root, params string[] paths)
    {
        var list = new List<SyncFile>();
        foreach (var path in paths)
        {
            list.Add(new SyncFile(RootedPath.Create(root, path, PathOptions)));
        }
        return list.ToArray();
    }

    public void AssertEqualPathCollection(IEnumerable<SyncFile> expected, IEnumerable<SyncFile> actual)
    {
        Assert.Equal(expected.Select(f => f.Path.ToString()), actual.Select(f => f.Path.ToString()));
    }
}