namespace FishSyncClient.FileComparers;

public interface IFileComparerFactory
{
    IFileComparer CreateFullComparer();
    IFileComparer CreateFastComparer();
}

public class LocalFileComparerFactory : IFileComparerFactory
{
    public IFileComparer CreateFullComparer()
    {
        return new LocalFileChecksumComparer();
    }

    public IFileComparer CreateFastComparer()
    {
        return new LocalFileSizeComparer();
    }
}