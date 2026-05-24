using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;

namespace FishSyncClientTest;

public class LocalSyncerTests
{
    [Fact]
    public async Task compare_and_sync_creates_added_file_and_parent_directories()
    {
        var root = createTempDirectory();
        try
        {
            var source = new MemorySourceSyncFile("nested/file.txt", "new content");
            var syncer = createSyncer(root);

            var result = await syncer.CompareAndSyncFiles(
                new[] { source },
                new LocalFileSizeComparer(),
                options: null);

            var targetPath = Path.Combine(root, "nested", "file.txt");
            Assert.True(File.Exists(targetPath));
            Assert.Equal("new content", await File.ReadAllTextAsync(targetPath));
            AssertEqualSubPaths(new[] { "nested/file.txt" }, result.AddedFiles);
            AssertEqualSubPaths(new[] { "nested/file.txt" }, result.UpdatedFilePairs.Select(pair => pair.Source));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task compare_and_sync_overwrites_updated_file()
    {
        var root = createTempDirectory();
        try
        {
            await File.WriteAllTextAsync(Path.Combine(root, "file.txt"), "old");
            var source = new MemorySourceSyncFile("file.txt", "new content");
            var syncer = createSyncer(root);

            var result = await syncer.CompareAndSyncFiles(
                new[] { source },
                new LocalFileSizeComparer(),
                options: null);

            Assert.Equal("new content", await File.ReadAllTextAsync(Path.Combine(root, "file.txt")));
            Assert.Empty(result.AddedFiles);
            AssertEqualSubPaths(new[] { "file.txt" }, result.UpdatedFilePairs.Select(pair => pair.Source));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task compare_and_sync_reports_deleted_files_without_deleting_them()
    {
        var root = createTempDirectory();
        try
        {
            var targetPath = Path.Combine(root, "target-only.txt");
            await File.WriteAllTextAsync(targetPath, "target content");
            var syncer = createSyncer(root);

            var result = await syncer.CompareAndSyncFiles(
                Array.Empty<SyncFile>(),
                new LocalFileSizeComparer(),
                options: null);

            Assert.True(File.Exists(targetPath));
            AssertEqualSubPaths(new[] { "target-only.txt" }, result.DeletedFiles);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static LocalSyncer createSyncer(string root)
    {
        return new LocalSyncer(
            root,
            new PathOptions(),
            new ParallelSyncFilePairSyncer(1));
    }

    private static string createTempDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void AssertEqualSubPaths(IEnumerable<string> expected, IEnumerable<SyncFile> actual)
    {
        Assert.Equal(expected.ToHashSet(), actual.Select(file => file.Path.SubPath).ToHashSet());
    }

    private sealed class MemorySourceSyncFile : SyncFile
    {
        private readonly byte[] _contents;

        public MemorySourceSyncFile(string subPath, string content)
            : base(RootedPath.FromSubPath(subPath, new PathOptions()))
        {
            _contents = System.Text.Encoding.UTF8.GetBytes(content);
            Metadata = new SyncFileMetadata
            {
                Size = _contents.Length
            };
        }

        public override bool IsReadable => true;
        public override bool IsWritable => false;

        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default) =>
            new(new MemoryStream(_contents, writable: false));

        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public override async Task CopyTo(
            Stream destination,
            IProgress<ByteProgress>? progress,
            CancellationToken cancellationToken)
        {
            await destination.WriteAsync(_contents, 0, _contents.Length, cancellationToken);
            progress?.Report(new ByteProgress(0, _contents.Length));
        }
    }
}
