using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class FileSizeComparer : IFileComparer
{
    public ValueTask<bool> CompareFile(FishPathPair pair)
    {
        var sourceSize = getSize(pair.Source);
        var targetSize = getSize(pair.Target);
        return new ValueTask<bool>(sourceSize == targetSize);
    }

    private long getSize(FishPath path)
    {
        if (path is FishFileMetadata metadata)
        {
            return metadata.Size;
        }
        else if (path.Path.IsRooted)
        {
            var fileInfo = new FileInfo(path.Path.GetFullPath());
            return fileInfo.Length;
        }
        else
        {
            return 0;
        }
    }
}
