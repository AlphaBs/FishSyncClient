namespace FishSyncClient.FileComparers;

public class FileSizeComparer : IFileComparer
{
    public ValueTask<bool> CompareFile(string path, FishFileMetadata file)
    {
        if (file.Size <= 0)
            return new ValueTask<bool>(true);

        var fileInfo = new FileInfo(path);
        var result = fileInfo.Length == file.Size;
        return new ValueTask<bool>(result);
    }
}
