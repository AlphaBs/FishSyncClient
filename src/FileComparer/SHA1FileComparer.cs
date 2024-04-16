namespace FishSyncClient.FileComparers;

public class SHA1FileComparer : ChecksumComparerBase
{
    public static readonly string AlgorithmName = "sha1";

    protected override string ComputeChecksum(string fullPath)
    {
        using var fileStream = File.OpenRead(fullPath);
        return ChecksumAlgorithms.ComputeSHA1(fileStream);
    }

    protected override bool IsSupportedAlgorithmName(string algorithmName)
    {
        return algorithmName == AlgorithmName;
    }
}
