using FishSyncClient.FileComparers;

namespace FishSyncClient.Syncer;

public static class FishFileSyncerFactory
{
    public static IFishFileSyncer CreateWithFullComparer()
    {
        return new FishFileSyncer(new IFileComparer[]
        {
            new FileSizeComparer(),
            createChecksumComparer()
        });
    }

    private static IFileComparer createChecksumComparer()
    {
        var comparer = new FileChecksumComparer();
        comparer.AddAlgorithm(MD5FileComparer.AlgorithmName, new MD5FileComparer());
        comparer.AddAlgorithm(SHA1FileComparer.AlgorithmName, new SHA1FileComparer());
        return comparer;
    }

    public static IFishFileSyncer CreateWithSizeComparer()
    {
        return new FishFileSyncer(new IFileComparer[]
        {
            new FileSizeComparer()
        });
    }

    public static IFishFileSyncer CreateWithChecksumComparer()
    {
        return new FishFileSyncer(new IFileComparer[]
        {
            createChecksumComparer()
        });
    }
}