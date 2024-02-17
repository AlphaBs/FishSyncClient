using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClient.Syncer;

public class FishFileSyncer
{
    public async ValueTask<FishFileSyncResult> Sync(
        IEnumerable<FishPathPair> files,
        IFileComparer comparer,
        IProgress<FishFileProgressEventArgs>? progress)
    {
        var updated = new List<FishPathPair>();
        var identical = new List<FishPathPair>();

        var filesArr = files.ToArray();
        for (int i = 0; i < filesArr.Length; i++)
        {
            var file = filesArr[i];
            progress?.Report(new FishFileProgressEventArgs(i + 1, filesArr.Length, file.Source.Path));

            var isIdenticalFile = await comparer.CompareFile(file);
            if (isIdenticalFile)
                identical.Add(file);
            else
                updated.Add(file);
        }

        if (filesArr.Any())
            progress?.Report(new FishFileProgressEventArgs(filesArr.Length, filesArr.Length, filesArr.Last().Source.Path));
        return new FishFileSyncResult(updated.ToArray(), identical.ToArray());
    }
}

public class FishFileSyncResult
{
    public FishFileSyncResult(FishPathPair[] updated, FishPathPair[] identical)
    {
        UpdatedFiles = updated;
        IdenticalFiles = identical;
    }

    public FishPathPair[] UpdatedFiles { get; }
    public FishPathPair[] IdenticalFiles { get; }
}