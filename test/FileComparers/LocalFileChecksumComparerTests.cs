using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using System.Text;

namespace FishSyncClientTest.FileComparers;

public class LocalFileChecksumComparerTests
{
    [Fact]
    public async Task return_not_equal_when_source_checksum_exists_and_target_does_not_exist()
    {
        var root = createTempDirectory();
        try
        {
            var source = createSource("file.txt", ChecksumAlgorithmNames.MD5, "anything");
            var target = new LocalSyncFile(RootedPath.Create(root, "file.txt", new PathOptions()));
            var comparer = new LocalFileChecksumComparer();

            var result = await comparer.AreEqual(new SyncFilePair(source, target), default);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task return_equal_when_source_checksum_matches_target_content()
    {
        var root = createTempDirectory();
        try
        {
            var content = "target content";
            await File.WriteAllTextAsync(Path.Combine(root, "file.txt"), content);
            var source = createSource(
                "file.txt",
                ChecksumAlgorithmNames.MD5,
                computeHash(ChecksumAlgorithmNames.MD5, content));
            var target = new LocalSyncFile(RootedPath.Create(root, "file.txt", new PathOptions()));
            var comparer = new LocalFileChecksumComparer();

            var result = await comparer.AreEqual(new SyncFilePair(source, target), default);

            Assert.True(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task return_not_equal_when_source_checksum_does_not_match_target_content()
    {
        var root = createTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "file.txt"), "target content");
            var source = createSource("file.txt", ChecksumAlgorithmNames.MD5, "not-the-target-checksum");
            var target = new LocalSyncFile(RootedPath.Create(root, "file.txt", new PathOptions()));
            var comparer = new LocalFileChecksumComparer();

            var result = await comparer.AreEqual(new SyncFilePair(source, target), default);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task throw_when_source_checksum_algorithm_is_not_supported()
    {
        var root = createTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "file.txt"), "target content");
            var source = createSource("file.txt", "unknown", "anything");
            var target = new LocalSyncFile(RootedPath.Create(root, "file.txt", new PathOptions()));
            var comparer = new LocalFileChecksumComparer();

            await Assert.ThrowsAsync<KeyNotFoundException>(async () =>
                await comparer.AreEqual(new SyncFilePair(source, target), default));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task return_equal_when_source_checksum_is_missing_and_target_exists()
    {
        var root = createTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "file.txt"), "target content");
            var source = new VirtualSyncFile(RootedPath.FromSubPath("file.txt", new PathOptions()));
            var target = new LocalSyncFile(RootedPath.Create(root, "file.txt", new PathOptions()));
            var comparer = new LocalFileChecksumComparer();

            var result = await comparer.AreEqual(new SyncFilePair(source, target), default);

            Assert.True(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task return_equal_when_source_checksum_algorithm_is_missing_and_target_exists()
    {
        var root = createTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "file.txt"), "target content");
            var source = createSource("file.txt", "", "anything");
            var target = new LocalSyncFile(RootedPath.Create(root, "file.txt", new PathOptions()));
            var comparer = new LocalFileChecksumComparer();

            var result = await comparer.AreEqual(new SyncFilePair(source, target), default);

            Assert.True(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task return_not_equal_when_source_checksum_is_missing_and_target_does_not_exist()
    {
        var root = createTempDirectory();
        try
        {
            var source = new VirtualSyncFile(RootedPath.FromSubPath("file.txt", new PathOptions()));
            var target = new LocalSyncFile(RootedPath.Create(root, "file.txt", new PathOptions()));
            var comparer = new LocalFileChecksumComparer();

            var result = await comparer.AreEqual(new SyncFilePair(source, target), default);

            Assert.False(result);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string createTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static VirtualSyncFile createSource(string subPath, string algorithmName, string checksum)
    {
        return new VirtualSyncFile(RootedPath.FromSubPath(subPath, new PathOptions()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum(algorithmName, checksum)
            }
        };
    }

    private static string computeHash(string algorithmName, string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return ChecksumAlgorithms.ComputeHash(algorithmName, stream);
    }
}
