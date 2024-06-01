using System.Security.Cryptography;

namespace FishSyncClient;

public static class ChecksumAlgorithms
{
    public static HashAlgorithm CreateHashAlgorithmFromName(string name)
    {
        if (name == ChecksumAlgorithmNames.MD5)
        {
            return MD5.Create();
        }
        else if (name == ChecksumAlgorithmNames.SHA1)
        {
            return SHA1.Create();
        }
        else
        {
            throw new KeyNotFoundException();
        }
    }

    public static string ComputeHash(string algName, Stream stream)
    {
        using var hashAlgorithm = CreateHashAlgorithmFromName(algName);
        var checksum = hashAlgorithm.ComputeHash(stream);
        return HashHelper.ToHexString(checksum);
    }

    public static string ComputeMD5(Stream stream) => 
        ComputeHash(ChecksumAlgorithmNames.MD5, stream);

    public static string ComputeSHA1(Stream stream) => 
        ComputeHash(ChecksumAlgorithmNames.SHA1, stream);
}