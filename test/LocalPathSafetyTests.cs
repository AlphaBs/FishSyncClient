using System.Diagnostics;
using FishSyncClient;
using FishSyncClient.Files;
using FishSyncClient.Syncer;

namespace FishSyncClientTest;

public class LocalPathSafetyTests : IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "fishsync-links-" + Guid.NewGuid().ToString("N"));
    private string Root => Path.Combine(_base, "root");
    private string Outside => Path.Combine(_base, "outside");
    private string Link => Path.Combine(Root, "linked");
    private static readonly PathOptions Options = new();

    public LocalPathSafetyTests()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Outside);
        File.WriteAllText(Path.Combine(Outside, "keep.txt"), "outside");
    }

    public void Dispose() => Directory.Delete(_base, true);

    [Fact]
    public void enumeration_rejects_directory_links_before_returning_external_files()
    {
        CreateDirectoryLink();
        Assert.Throws<IOException>(() => RootedPath.FromDirectory(Root, Options).ToArray());
        AssertOutsideUnchanged();
    }

    [Fact]
    public async Task read_write_and_delete_reject_linked_ancestor()
    {
        var syncer = CreateSyncer();
        CreateDirectoryLink();
        var file = new LocalSyncFile(RootedPath.Create(Root, "linked/keep.txt", Options));

        await Assert.ThrowsAsync<IOException>(async () => { using var stream = await file.OpenReadStream(default); });
        await Assert.ThrowsAsync<IOException>(async () => { using var stream = await file.OpenWriteStream(default); });
        Assert.Throws<IOException>(() => syncer.DeleteLocalFiles([file]));
        AssertOutsideUnchanged();
    }

    [Fact]
    public void root_and_root_ancestors_must_not_be_links()
    {
        CreateDirectoryLink();
        Assert.Throws<IOException>(() => new LocalSyncer(Link, Options, new ParallelSyncFilePairSyncer(1)));
        Assert.Throws<IOException>(() => new LocalSyncer(Path.Combine(Link, "child"), Options, new ParallelSyncFilePairSyncer(1)));
    }

    [Fact]
    public void delete_rechecks_a_directory_changed_after_enumeration()
    {
        Directory.CreateDirectory(Link);
        File.WriteAllText(Path.Combine(Link, "keep.txt"), "inside");
        var syncer = CreateSyncer();
        var files = LocalSyncer.EnumerateLocalSyncFiles(Root, Options).ToArray();
        Directory.Delete(Link, true);
        CreateDirectoryLink();

        Assert.Throws<IOException>(() => syncer.DeleteLocalFiles(files));
        AssertOutsideUnchanged();
    }

    [Fact]
    public async Task staged_transfer_rejects_linked_destination()
    {
        CreateDirectoryLink();
        var source = new LocalSyncFile(RootedPath.Create(Outside, "keep.txt", Options));
        var target = new LocalSyncFile(RootedPath.Create(Root, "linked/keep.txt", Options));
        await Assert.ThrowsAsync<IOException>(() => new SyncFilePair(source, target).SyncContent());
        AssertOutsideUnchanged();
    }

    [UnixOnlyTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task file_links_including_dangling_links_are_rejected(bool dangling)
    {
        var target = Path.Combine(Outside, dangling ? "missing.txt" : "keep.txt");
        var filePath = Path.Combine(Root, "file-link");
        File.CreateSymbolicLink(filePath, target);
        var file = new LocalSyncFile(RootedPath.Create(Root, "file-link", Options));

        Assert.Throws<IOException>(() => RootedPath.FromDirectory(Root, Options).ToArray());
        await Assert.ThrowsAsync<IOException>(async () => { using var stream = await file.OpenWriteStream(default); });
        Assert.Throws<IOException>(() => CreateSyncer().DeleteLocalFiles([file]));
        if (dangling) Assert.False(File.Exists(target));
        AssertOutsideUnchanged();
    }

    private LocalSyncer CreateSyncer() => new(Root, Options, new ParallelSyncFilePairSyncer(1));
    private void AssertOutsideUnchanged() => Assert.Equal("outside", File.ReadAllText(Path.Combine(Outside, "keep.txt")));

    private void CreateDirectoryLink()
    {
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateSymbolicLink(Link, Outside);
            return;
        }

        // Directory junctions exercise reparse-point handling without requiring symlink privileges.
        using var process = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{Link}\" \"{Outside}\"")
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true
        })!;
        Assert.True(process.WaitForExit(10000));
        Assert.Equal(0, process.ExitCode);
    }
}
