namespace FishSyncClient.FileComparers;

public static class FileComparerFactory
{
    public static IFileComparer CreateChecksumComparer()
    {
        var comparer = new FileChecksumComparer();
        comparer.AddAlgorithm(MD5FileComparer.AlgorithmName, new MD5FileComparer());
        comparer.AddAlgorithm(SHA1FileComparer.AlgorithmName, new SHA1FileComparer());
        return comparer;
    }

    public static IFileComparer CreateSizeComparer()
    {
        return new FileSizeComparer();
    }
}