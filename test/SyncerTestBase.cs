using FishSyncClient;
using FishSyncClient.Files;
using System.Runtime.InteropServices;

namespace FishSyncClientTest;

public class SyncerTestBase
{
    public PathOptions PathOptions = new PathOptions
    {
        PathSeparator = '/',
        AltPathSeparator = '\\',
    };

    private string getOSRoot() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "C:\\" : "/";

    public SyncFile[] CreateSourcePaths(params string[] paths)
    {
        return CreatePathsFromRoot(getOSRoot() + "source", paths);
    }

    public SyncFile[] CreateTargetPaths(params string[] paths)
    {
        return CreatePathsFromRoot(getOSRoot() + "target", paths);
    }

    public SyncFile[] CreatePathsFromRoot(string root, params string[] paths)
    {
        var list = new List<SyncFile>();
        foreach (var path in paths)
        {
            list.Add(new VirtualSyncFile(RootedPath.Create(root, path, PathOptions)));
        }
        return list.ToArray();
    }

    public void AssertEqualPathCollection(IEnumerable<SyncFile> expected, IEnumerable<SyncFile> actual)
    {
        Assert.Equal(
            expected.Select(f => f.Path.ToString()).ToHashSet(), 
            actual.Select(f => f.Path.ToString()).ToHashSet());
    }
}