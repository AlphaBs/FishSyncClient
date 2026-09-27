using System.Net;
using System.Text;
using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;

namespace FishSyncClientTest.Files;

public class AtomicLocalTransferTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "fishsync-atomic-" + Guid.NewGuid().ToString("N"));
    private string TargetPath => Path.Combine(_root, "target", "payload.bin");
    private LocalSyncFile Target => new(RootedPath.Create(Path.Combine(_root, "target"), "payload.bin", new PathOptions()));

    public AtomicLocalTransferTests() => Directory.CreateDirectory(Path.GetDirectoryName(TargetPath)!);
    public void Dispose() => Directory.Delete(_root, true);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task http_error_preserves_existing_file_or_leaves_new_file_absent(bool exists)
    {
        if (exists) File.WriteAllText(TargetPath, "original");
        using var client = new HttpClient(new MissingHandler());
        var source = new ReadableHttpSyncFile(RootedPath.FromSubPath("payload.bin", new PathOptions()), client)
        {
            Location = new Uri("https://example.invalid/payload.bin")
        };

        await Assert.ThrowsAsync<HttpRequestException>(() => new SyncFilePair(source, Target).SyncContent());

        if (exists) Assert.Equal("original", File.ReadAllText(TargetPath));
        else Assert.False(File.Exists(TargetPath));
        AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task partial_transfer_failure_or_cancellation_preserves_original(bool cancel)
    {
        File.WriteAllText(TargetPath, "original");
        using var cancellation = new CancellationTokenSource();
        var source = new CallbackSource(async (stream, token) =>
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("partial"), token);
            if (cancel)
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
            }
            throw new IOException("transfer failed");
        });

        var operation = new SyncFilePair(source, Target).SyncContent(cancellationToken: cancellation.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        else await Assert.ThrowsAsync<IOException>(() => operation);

        Assert.Equal("original", File.ReadAllText(TargetPath));
        AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task failed_integrity_check_does_not_publish_staged_content(bool exists)
    {
        if (exists) File.WriteAllText(TargetPath, "original");
        var source = CreateSource("new content");
        source.Metadata!.Checksum = new("md5", "wrong checksum");

        var exception = await Assert.ThrowsAsync<FileIntegrityException>(() => Sync(source, new LocalFileChecksumComparer()));

        Assert.Equal(Target.Path.ToString(), exception.File);
        if (exists) Assert.Equal("original", File.ReadAllText(TargetPath));
        else Assert.False(File.Exists(TargetPath));
        AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData("md5")]
    [InlineData("sha1")]
    public async Task valid_uppercase_checksum_is_verified_before_replacing_original(string algorithm)
    {
        File.WriteAllText(TargetPath, "original");
        var source = CreateSource("new content");
        using (var content = new MemoryStream(Encoding.UTF8.GetBytes("new content")))
            source.Metadata!.Checksum = new(algorithm, ChecksumAlgorithms.ComputeHash(algorithm, content).ToUpperInvariant());

        var result = await Sync(source, new InspectingComparer(TargetPath));

        Assert.Single(result.UpdatedFiles);
        Assert.Equal("new content", File.ReadAllText(TargetPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task cancellation_after_copy_but_before_commit_preserves_original()
    {
        File.WriteAllText(TargetPath, "original");
        using var cancellation = new CancellationTokenSource();
        var source = new CallbackSource(async (stream, token) =>
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("replacement"), token);
            cancellation.Cancel();
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SyncFilePair(source, Target).SyncContent(cancellationToken: cancellation.Token));

        Assert.Equal("original", File.ReadAllText(TargetPath));
        AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task copying_a_local_file_to_itself_preserves_content()
    {
        File.WriteAllText(TargetPath, "original");
        await new SyncFilePair(Target, Target).SyncContent();
        Assert.Equal("original", File.ReadAllText(TargetPath));
        AssertNoTemporaryFiles();
    }

    private Task<SyncFilePairCollectionCompareResult> Sync(SyncFile source, IFileComparer comparer) =>
        new ParallelSyncFilePairSyncer(1).CompareAndSyncFilePairs([new(source, Target)], comparer, null, null, default);

    private LocalSyncFile CreateSource(string text)
    {
        var directory = Path.Combine(_root, "source");
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "payload.bin"), text);
        return new LocalSyncFile(RootedPath.Create(directory, "payload.bin", new PathOptions()))
        {
            Metadata = new() { Size = Encoding.UTF8.GetByteCount(text) }
        };
    }

    private void AssertNoTemporaryFiles() => Assert.Empty(Directory.GetFiles(_root, ".fishsync-*.tmp", SearchOption.AllDirectories));

    private sealed class MissingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }

    private sealed class InspectingComparer(string targetPath) : IFileComparer
    {
        public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
        {
            // Both pre-copy comparison and post-copy verification must see the original still installed.
            Assert.Equal("original", File.ReadAllText(targetPath));
            return await new LocalFileChecksumComparer().AreEqual(pair, cancellationToken);
        }
    }

    private sealed class CallbackSource(Func<Stream, CancellationToken, Task> copy)
        : SyncFile(RootedPath.FromSubPath("payload.bin", new PathOptions()))
    {
        public override bool IsReadable => true;
        public override bool IsWritable => false;
        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public override Task CopyTo(Stream destination, IProgress<ByteProgress>? progress, CancellationToken cancellationToken) => copy(destination, cancellationToken);
    }
}
