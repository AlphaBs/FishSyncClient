namespace FishSyncClient.Syncer;

public class FishFileSyncer
{
    private readonly IEnumerable<IFileComparer> _comparers;

    public FishFileSyncer(IEnumerable<IFileComparer> comparers)
    {
        _comparers = comparers;
    }

    public async ValueTask<FishFileSyncResult> Sync(string root, IEnumerable<FishFileMetadata> files)
    {
        var updated = new List<FishFileMetadata>();
        var identical = new List<FishFileMetadata>();

        foreach (var file in files)
        {
            var result = await compareFile(root, file);
            if (result)
                updated.Add(file);
            else
                identical.Add(file);
        }

        return new FishFileSyncResult(updated.ToArray(), identical.ToArray());
    }

    private async ValueTask<bool> compareFile(string root, FishFileMetadata file)
    {
        var fullPath = file.Path.WithRoot(root).GetFullPath();
        foreach (var comparer in _comparers)
        {
            var result = await comparer.CompareFile(fullPath, file);
            if (!result)
                return false;
        }
        return true;
    }
}
