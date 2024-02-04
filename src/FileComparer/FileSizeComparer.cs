namespace FishSyncClient;

public class FileSizeComparer : IFileComparer
{
    public ValueTask<bool> CompareFile(string path, FishFileMetadata file)
    {
        var fileInfo = new FileInfo(path);
        var result = fileInfo.Length == file.Size;
        return new ValueTask<bool>(result);
    }
}
