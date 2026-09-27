using System.Text;

namespace FishSyncClient.Versions;

public class VersionManager : IVersionManager
{
    public const int FileSizeLimit = 128; // 128 byte

    private readonly string _versionPath;

    public VersionManager(string versionPath) => _versionPath = versionPath;

    public async Task<string?> GetCurrentVersion()
    {
        try
        {
            using var fs = File.OpenRead(this._versionPath);
            var buffer = new byte[FileSizeLimit + 1];
            var read = 0;
            while (read < buffer.Length)
            {
                var count = await fs.ReadAsync(buffer, read, buffer.Length - read);
                if (count == 0)
                    break;
                read += count;
            }
            if (read > FileSizeLimit)
                return null;
            var versionStr = Encoding.UTF8.GetString(buffer, 0, read);
            return versionStr.Trim();
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> CheckNewVersion(string? sourceVersion)
    {
        var currentVersion = await GetCurrentVersion();
        return currentVersion != sourceVersion?.Trim();
    }

    public async Task UpdateVersion(string newVersion)
    {
        newVersion = newVersion.Trim();
        var versionBytes = Encoding.UTF8.GetBytes(newVersion);
        if (versionBytes.Length > FileSizeLimit)
            throw new ArgumentException($"Version must not exceed {FileSizeLimit} UTF-8 bytes.", nameof(newVersion));
        using var fs = File.Create(this._versionPath);
        await fs.WriteAsync(versionBytes);
    }
}
