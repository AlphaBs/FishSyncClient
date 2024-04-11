using FishSyncClient.Common;

namespace FishSyncClient.FileComparers;

public class MD5FileComparer : ChecksumComparerBase
{
    public static readonly string AlgorithmName = "md5";

    protected override string ComputeChecksum(string fullPath)
    {
        using var fileStream = File.OpenRead(fullPath);
        return ChecksumAlgorithms.ComputeMD5(fileStream);
    }

    protected override bool IsSupportedAlgorithmName(string algorithmName)
    {
        return algorithmName == AlgorithmName;
    }
}
