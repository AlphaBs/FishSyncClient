using FishSyncClient.FileComparers;

namespace FishSyncClient.Syncer;

public class FishFileSyncer : IFishFileSyncer
{
    private readonly IEnumerable<IFileComparer> _comparers;

    public FishFileSyncer(IEnumerable<IFileComparer> comparers)
    {
        _comparers = comparers;
    }

    public async ValueTask<FishFileSyncResult> Sync(
        string root, 
        IEnumerable<FishFileMetadata> files,
        IProgress<FishFileProgressEventArgs>? progress)
    {
        var updated = new List<FishFileMetadata>();
        var identical = new List<FishFileMetadata>();

        var filesArr = files.ToArray();
        for (int i = 0; i < filesArr.Length; i++)
        {
            var file = filesArr[i];
            progress?.Report(new FishFileProgressEventArgs(i + 1, filesArr.Length, file.Path));

            var result = await compareFile(root, file);
            if (result)
                identical.Add(file);
            else
                updated.Add(file);
        }

        if (filesArr.Any())
            progress?.Report(new FishFileProgressEventArgs(filesArr.Length, filesArr.Length, filesArr.Last().Path));
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
