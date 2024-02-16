using FishSyncClient.FileComparers;

namespace FishSyncClient.Syncer;

public class FishFileSyncer
{
    public async ValueTask<FishFileSyncResult> Sync(
        string root, 
        IEnumerable<FishFileMetadata> files,
        IFileComparer comparer,
        IProgress<FishFileProgressEventArgs>? progress)
    {
        var updated = new List<FishFileMetadata>();
        var identical = new List<FishFileMetadata>();

        var filesArr = files.ToArray();
        for (int i = 0; i < filesArr.Length; i++)
        {
            var file = filesArr[i];
            progress?.Report(new FishFileProgressEventArgs(i + 1, filesArr.Length, file.Path));

            var fullPath = file.Path.WithRoot(root).GetFullPath();
            var isIdenticalFile = await comparer.CompareFile(fullPath, file);

            if (isIdenticalFile)
                identical.Add(file);
            else
                updated.Add(file);
        }

        if (filesArr.Any())
            progress?.Report(new FishFileProgressEventArgs(filesArr.Length, filesArr.Length, filesArr.Last().Path));
        return new FishFileSyncResult(updated.ToArray(), identical.ToArray());
    }
}

public class FishFileSyncResult
{
    public FishFileSyncResult(FishFileMetadata[] updated, FishFileMetadata[] identical)
    {
        UpdatedFiles = updated;
        IdenticalFiles = identical;
    }

    public FishFileMetadata[] UpdatedFiles { get; }
    public FishFileMetadata[] IdenticalFiles { get; }
}