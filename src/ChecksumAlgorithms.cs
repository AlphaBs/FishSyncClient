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
            throw new KeyNotFoundException(name);
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

    public static async Task<string> ComputeHashAsync(
        string algName, Stream stream, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var algorithm = CreateHashAlgorithmFromName(algName);
        var buffer = new byte[65536];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (read == 0)
                break;
            algorithm.TransformBlock(buffer, 0, read, buffer, 0);
        }
        algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return HashHelper.ToHexString(algorithm.Hash!);
    }
}
