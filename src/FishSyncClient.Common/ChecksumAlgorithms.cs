using System.Security.Cryptography;

namespace FishSyncClient.Common;

public static class ChecksumAlgorithms
{
    public static string ComputeMD5(Stream stream)
    {
        using var md5 = MD5.Create();
        var checksum = md5.ComputeHash(stream);
        return HashHelper.ToHexString(checksum);
    }

    public static string ComputeSHA1(Stream stream)
    {
        using var sha1 = SHA1.Create();
        var checksum = sha1.ComputeHash(stream);
        return HashHelper.ToHexString(checksum);
    }
}