namespace FishSyncClient.FileComparers;

public class FileChecksumComparer : IFileComparer
{
    private readonly Dictionary<string, IFileComparer> _comparers = new();

    public IFileComparer? DefaultComparer { get; set; }

    public void AddAlgorithm(string algorithmName, IFileComparer comparer)
    {
        _comparers.Add(algorithmName, comparer);
    }

    public IFileComparer? GetComparer(string checksumAlgorithm)
    {
        if (_comparers.TryGetValue(checksumAlgorithm, out var comparer))
        {
            return comparer;
        }
        else
        {
            return null;
        }
    }

    public async ValueTask<bool> CompareFile(string path, FishFileMetadata file)
    {
        if (string.IsNullOrEmpty(file.ChecksumAlgorithm))
            return true;

        var comparer = GetComparer(file.ChecksumAlgorithm) ?? DefaultComparer;
        if (comparer == null)
        {
            throw new Exception("Unsupported algorithm name: " + file.ChecksumAlgorithm);
        }

        return await comparer.CompareFile(path, file);
    }
}