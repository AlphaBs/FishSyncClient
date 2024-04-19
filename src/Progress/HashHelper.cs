namespace FishSyncClient.Progress;

public static class HashHelper
{
    public static string ToHexString(byte[] data)
    {
        return BitConverter.ToString(data).Replace("-", "").ToLowerInvariant();
    }
}
