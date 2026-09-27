using System.Text;
using FishSyncClient.Versions;

namespace FishSyncClientTest.Versions;

public class VersionManagerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fishsync-version-" + Guid.NewGuid().ToString("N"));
    private string VersionPath => Path.Combine(_root, "version");
    public VersionManagerTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData("x", 128)]
    [InlineData("한", 42)]
    [InlineData("😀", 32)]
    public async Task supported_versions_roundtrip(string character, int count)
    {
        var value = string.Concat(Enumerable.Repeat(character, count));
        var manager = new VersionManager(VersionPath);
        await manager.UpdateVersion(value);
        Assert.Equal(value, await manager.GetCurrentVersion());
        Assert.False(await manager.CheckNewVersion(value));
    }

    [Theory]
    [InlineData("x", 129)]
    [InlineData("한", 43)]
    [InlineData("😀", 33)]
    public async Task oversized_input_is_rejected_before_modifying_existing_version(string character, int count)
    {
        var manager = new VersionManager(VersionPath);
        await manager.UpdateVersion("original");
        var value = string.Concat(Enumerable.Repeat(character, count));
        await Assert.ThrowsAsync<ArgumentException>(() => manager.UpdateVersion(value));
        Assert.Equal("original", await manager.GetCurrentVersion());
    }

    [Fact]
    public async Task oversized_existing_file_is_invalid_instead_of_truncated()
    {
        await File.WriteAllTextAsync(VersionPath, new string('x', 129), Encoding.UTF8);
        Assert.Null(await new VersionManager(VersionPath).GetCurrentVersion());
    }

    [Fact]
    public async Task comparison_uses_the_same_whitespace_normalization_as_storage()
    {
        var manager = new VersionManager(VersionPath);
        await manager.UpdateVersion(" version-1\n");
        Assert.False(await manager.CheckNewVersion(" version-1\n"));
        Assert.False(await manager.CheckNewVersion("version-1"));
    }
}
