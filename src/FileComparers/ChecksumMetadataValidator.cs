using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

internal static class ChecksumMetadataValidator
{
    public static void Validate(SyncFileChecksum checksum, string role)
    {
        int expectedLength;
        try
        {
            using var algorithm = ChecksumAlgorithms.CreateHashAlgorithmFromName(checksum.AlgorithmName);
            expectedLength = algorithm.HashSize / 4;
        }
        catch (KeyNotFoundException)
        {
            throw new FileComparerException($"Unsupported {role} checksum algorithm: '{checksum.AlgorithmName}'.");
        }

        var hex = checksum.ChecksumHexString;
        if (hex is null || hex.Length != expectedLength)
            throw new FileComparerException($"The {role} checksum must contain exactly {expectedLength} hexadecimal characters.");

        foreach (var character in hex)
        {
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F'))
                throw new FileComparerException($"The {role} checksum contains a non-hexadecimal character.");
        }
    }
}
