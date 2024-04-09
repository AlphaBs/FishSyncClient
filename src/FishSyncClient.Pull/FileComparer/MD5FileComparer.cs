using System.Security.Cryptography;

namespace FishSyncClient.FileComparers;

public class MD5FileComparer : ChecksumComparerBase
{
    public static readonly string AlgorithmName = "md5";

    protected override string ComputeChecksum(string fullPath)
    {
        using var fileStream = File.OpenRead(fullPath);
        using var md5 = MD5.Create();
        var checksum = md5.ComputeHash(fileStream);
        return HashHelper.ToHexString(checksum);
    }

    protected override bool IsSupportedAlgorithmName(string algorithmName)
    {
        return algorithmName == AlgorithmName;
    }
}
