using System.Security.Cryptography;

namespace FishSyncClient.FileComparers;

public class MD5FileComparer : IFileComparer
{
    public static readonly string AlgorithmName = "md5";

    public ValueTask<bool> CompareFile(string path, FishFileMetadata file)
    {
        if (file.ChecksumAlgorithm != AlgorithmName)
            throw new InvalidOperationException("Not supported algorithm");

        using var fileStream = File.OpenRead(path);
        using var md5 = MD5.Create();
        var checksum = md5.ComputeHash(fileStream);
        
        var result = HashHelper.ToHexString(checksum) == file.Checksum;
        return new ValueTask<bool>(result);
    }
}
