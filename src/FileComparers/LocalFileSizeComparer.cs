using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class LocalFileSizeComparer : IFileComparer
{
    public ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        if (pair.Target is LocalSyncFile targetLocalFile)
        {
            if (targetLocalFile.Exists == false)
                return new ValueTask<bool>(false);

            var areEqual = getSourceSize(pair.Source) == getTargetSize(targetLocalFile);
            return new ValueTask<bool>(areEqual);
        }
        else
        {
            throw new FileComparerException("Target should be LocalSyncFile");
        }
    }

    private long getSourceSize(SyncFile source)
    {
        var size = source.Metadata?.Size ?? -1;
        if (size < 0)
            throw new FileComparerException();
        return size;
    }

    private long getTargetSize(LocalSyncFile target)
    {
        var file = new FileInfo(target.Path.GetFullPath());
        return file.Length;
    }
}