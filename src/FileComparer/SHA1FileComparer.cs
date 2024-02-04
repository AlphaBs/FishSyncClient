
using System.Security.Cryptography;

namespace FishSyncClient;

public class SHA1FileComparer : IFileComparer
{
    public static readonly string AlgorithmName = "sha1";

    public ValueTask<bool> CompareFile(string path, FishFileMetadata file)
    {
        if (file.ChecksumAlgorithm != AlgorithmName)
            return new ValueTask<bool>(true);

        using var fileStream = File.OpenRead(path);
        using var sha1 = SHA1.Create();
        var checksum = sha1.ComputeHash(fileStream);

        var result = HashHelper.ToHexString(checksum) == file.Checksum;
        return new ValueTask<bool>(result);
    }
}
