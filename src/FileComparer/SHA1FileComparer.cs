using System.Security.Cryptography;

namespace FishSyncClient.FileComparers;

public class SHA1FileComparer : ChecksumComparerBase
{
    public static readonly string AlgorithmName = "sha1";

    protected override string ComputeChecksum(string fullPath)
    {
        using var fileStream = File.OpenRead(fullPath);
        using var sha1 = SHA1.Create();
        var checksum = sha1.ComputeHash(fileStream);
        return HashHelper.ToHexString(checksum);
    }

    protected override bool IsSupportedAlgorithmName(string algorithmName)
    {
        return algorithmName == AlgorithmName;
    }
}
