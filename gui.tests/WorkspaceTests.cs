using FishSyncClient.Gui;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace FishSyncClient.Gui.Tests;

public sealed class TestDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "FishSyncClientTests", Guid.NewGuid().ToString("N"));
    public TestDirectory() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        WorkspaceFiles.EnsureNoLinks(Path);
        Directory.Delete(Path, true);
    }
}

public class WorkspaceTests
{
    internal static readonly DateTimeOffset Updated = new(2026, 9, 24, 1, 0, 0, TimeSpan.Zero);
    internal static RemoteEntry Remote(string path, string content) => new(path, "https://files.test/" + path,
        new(Encoding.UTF8.GetByteCount(content), Updated, Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(content)))));

    [Theory]
    [InlineData("../outside")]
    [InlineData("/absolute")]
    [InlineData("C:\\outside")]
    [InlineData("sub/../../outside")]
    [InlineData("file:stream")]
    [InlineData("CON")]
    [InlineData("trailing.")]
    public void RejectsPathsOutsideManagedWorkspace(string path)
    {
        using var directory = new TestDirectory();
        var workspace = new WorkspaceFiles(directory.Path, "bucket");
        Assert.Throws<IOException>(() => workspace.Resolve(path));
    }

    [Fact]
    public void RejectsUnsafeBucketIdAndDuplicateRemotePaths()
    {
        using var directory = new TestDirectory();
        Assert.Throws<IOException>(() => new WorkspaceFiles(directory.Path, "../other"));
        var workspace = new WorkspaceFiles(directory.Path, "bucket");
        Assert.Throws<IOException>(() => workspace.ValidateRemote(new("bucket", Updated, [Remote("a", "a"), Remote("a", "b")])));
        Assert.Throws<IOException>(() => workspace.ValidateRemote(new("bucket", Updated, [Remote("a", "a"), Remote("a/b", "b")])));
    }

    [Fact]
    public void ChecksumComparisonIgnoresTimestampAndDetectsSameSizeEdits()
    {
        var server = Remote("file", "abc");
        List<LocalEntry> local = [new("file", 3, Updated.UtcDateTime.AddDays(1), Checksum: server.Metadata.Checksum)];
        Assert.Empty(WorkspaceFiles.Compare(local, [server]));
        local = [local[0] with { LastUpdated = Updated.UtcDateTime, Checksum = Remote("file", "xyz").Metadata.Checksum }];
        Assert.Equal("갱신", Assert.Single(WorkspaceFiles.Compare(local, [server])).Kind);
        local = [local[0] with { LastUpdated = Updated.UtcDateTime.AddMinutes(-1) }];
        Assert.Equal("갱신", Assert.Single(WorkspaceFiles.Compare(local, [server])).Kind);
    }

    [Fact]
    public void ListRetainsDeletedFilesAndPreservesItemIdentity()
    {
        var items = new ObservableCollection<WorkspaceFileItem>();
        List<RemoteEntry> remote = [Remote("folder/deleted.txt", "old")];
        List<LocalEntry> local = [new("folder", 0, Updated.UtcDateTime, true), new("folder/new.txt", 3, Updated.UtcDateTime)];
        WorkspaceFileItem.Reconcile(items, local, remote);
        Assert.Equal(2, items.Count);
        var added = items.Single(x => x.Path == "folder/new.txt");
        Assert.Contains(items, x => x.Kind == "삭제");
        Assert.Equal("추가", added.Kind);
        WorkspaceFileItem.Reconcile(items, local, remote);
        Assert.Same(added, items.Single(x => x.Path == "folder/new.txt"));
        WorkspaceFileItem.Reconcile(items, local, []);
        Assert.Same(added, Assert.Single(items));
    }

    [Fact]
    public async Task SettingsRoundTripUsernameAndBucket()
    {
        using var directory = new TestDirectory();
        var path = System.IO.Path.Combine(directory.Path, "config", "config.json");
        var manager = new ConfigManager(path);
        var config = await manager.LoadConfig();
        config.Username = "tester";
        config.BucketId = "bucket";
        await manager.SaveConfig();
        var restored = await new ConfigManager(path).LoadConfig();
        Assert.Equal("tester", restored.Username);
        Assert.Equal("bucket", restored.BucketId);
        Assert.DoesNotContain("Password", await File.ReadAllTextAsync(path));
    }

    [Fact]
    public void ScanRehashesContentEvenWhenSizeAndTimestampAreUnchanged()
    {
        using var directory = new TestDirectory();
        var workspace = new WorkspaceFiles(directory.Path, "bucket");
        workspace.Prepare();
        var file = workspace.Resolve("file.txt");
        File.WriteAllText(file, "abc");
        File.SetLastWriteTimeUtc(file, Updated.UtcDateTime);
        var before = Assert.Single(workspace.Scan(default));
        File.WriteAllText(file, "xyz");
        File.SetLastWriteTimeUtc(file, Updated.UtcDateTime);
        var after = Assert.Single(workspace.Scan(default));
        Assert.Equal(before.Size, after.Size);
        Assert.Equal(before.LastUpdated, after.LastUpdated);
        Assert.NotEqual(before.Checksum, after.Checksum);
    }
}
