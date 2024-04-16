namespace FishSyncClient.FileComparers;

public interface IFileComparerFactory
{
    IFileComparer CreateFullComparer();
    IFileComparer CreateFastComparer();
}

public class DefaultFileComparerFactory : IFileComparerFactory
{
    public IFileComparer CreateFullComparer()
    {
        var comparer = new FileChecksumComparer();
        comparer.AddAlgorithm(MD5FileComparer.AlgorithmName, new MD5FileComparer());
        comparer.AddAlgorithm(SHA1FileComparer.AlgorithmName, new SHA1FileComparer());
        return comparer;
    }

    public IFileComparer CreateFastComparer()
    {
        return new FileSizeComparer();
    }
}