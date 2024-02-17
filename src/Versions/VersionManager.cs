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
            var buffer = new byte[FileSizeLimit];
            var read = await fs.ReadAsync(buffer, 0, buffer.Length);
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
        return currentVersion != sourceVersion;
    }

    public async Task UpdateVersion(string newVersion)
    {
        newVersion = newVersion.Trim();
        var versionBytes = Encoding.UTF8.GetBytes(newVersion);
        using var fs = File.Create(this._versionPath);
        await fs.WriteAsync(versionBytes);
    }
}