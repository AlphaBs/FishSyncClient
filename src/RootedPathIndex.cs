namespace FishSyncClient;

public class RootedPathIndex
{
    private readonly HashSet<string> _pathSet;

    public string Root { get; }
    public IEnumerable<RootedPath> Paths => 
        _pathSet.Select(subpath => RootedPath.Create(Root, subpath));

    public RootedPathIndex(string root, PathOptions pathOptions)
    {
        _pathSet = new HashSet<string>();
        Root = PathHelper.NormalizeDirectoryPath(root, pathOptions);
    }

    public static RootedPathIndex CreateFromDirectory(string root, PathOptions pathOptions)
    {
        var index = new RootedPathIndex(root, pathOptions);
        var files = Directory.GetFiles(index.Root, "*", SearchOption.AllDirectories);
        foreach (var item in files)
        {
            index.Add(RootedPath.FromFullPath(root, item));
        }
        return index;
    }

    public bool Add(RootedPath path)
    {
        if (path.IsRooted)
        {
            if (path.Root != this.Root)
                throw new ArgumentException();
        }

        return _pathSet.Add(path.SubPath);
    }

    public bool Remove(RootedPath path)
    {
        if (path.IsRooted)
        {
            if (path.Root != this.Root)
                throw new ArgumentException();
        }

        return _pathSet.Remove(path.SubPath);
    }
}
