using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Syncer;

namespace FishSyncClientTest.FileComparers;

public sealed class ChecksumContractTests : IDisposable
{
    private const string Md5 = "202cb962ac59075b964b07152d234b70";
    private readonly string _root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private readonly PathOptions _pathOptions = new();

    public ChecksumContractTests() => Directory.CreateDirectory(_root);

    public static IEnumerable<object?[]> InvalidChecksums()
    {
        yield return [null, null]; // default(SyncFileChecksum)도 포함한다.
        yield return [null, Md5];
        yield return ["", Md5];
        yield return [" ", Md5];
        yield return ["md5", null];
        yield return ["md5", ""];
        yield return ["md5", new string(' ', 32)];
        yield return ["md5", new string('g', 32)];
        yield return ["md5", new string('０', 32)];
        yield return ["md5", new string('a', 31)];
        yield return ["md5", new string('a', 33)];
        yield return ["sha1", new string('a', 39)];
        yield return ["sha1", new string('a', 41)];
        yield return ["sha1", new string('z', 40)];
        yield return ["unknown", Md5];
        yield return ["MD5", Md5]; // 알고리즘 이름은 계속 대소문자를 구분한다.
    }

    [Theory]
    [MemberData(nameof(InvalidChecksums))]
    public async Task invalid_source_fails_before_reading_or_writing_existing_or_missing_target(
        string? algorithm, string? hex)
    {
        foreach (var targetExists in new[] { false, true })
        {
            var source = Source(new SyncFileChecksum(algorithm!, hex!));
            var target = new TrackingLocalFile(RootedPath.Create(_root, "target.txt", _pathOptions));
            if (targetExists)
                await File.WriteAllTextAsync(target.Path.GetFullPath(), "preserve me");

            await Assert.ThrowsAsync<FileComparerException>(() =>
                new ParallelSyncFilePairSyncer(1).CompareAndSyncFilePairs(
                    [new SyncFilePair(source, target)], new LocalFileChecksumComparer(), null, null, default));

            Assert.Equal(0, source.ReadCount);
            Assert.Equal(0, target.ReadCount);
            Assert.Equal(0, target.WriteCount);
            Assert.Equal(targetExists, target.Exists);
            if (targetExists)
                Assert.Equal("preserve me", await File.ReadAllTextAsync(target.Path.GetFullPath()));
        }
    }

    [Theory]
    [MemberData(nameof(InvalidChecksums))]
    public async Task error_modes_do_not_accept_invalid_supplied_metadata(string? algorithm, string? hex)
    {
        foreach (var mode in Enum.GetValues<ComparerErrorHandlingModes>())
        {
            foreach (var invalidSource in new[] { false, true })
            {
                var valid = new SyncFileChecksum("md5", Md5);
                var invalid = new SyncFileChecksum(algorithm!, hex!);
                var source = Source(invalidSource ? invalid : valid);
                var target = Source(invalidSource ? valid : invalid);

                await Assert.ThrowsAsync<FileComparerException>(async () =>
                    await new FileChecksumMetadataComparer(mode).AreEqual(new(source, target), default));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task omitted_source_checksum_is_normal_in_every_metadata_error_mode(bool omitMetadata)
    {
        var source = Source(null);
        if (omitMetadata)
            source.Metadata = null;

        // 원본이 체크섬 검증을 요구하지 않으면 대상 체크섬을 요구하거나 검사하지 않는다.
        foreach (var targetChecksum in new SyncFileChecksum?[] { null, default(SyncFileChecksum), new("md5", Md5) })
        {
            var target = Source(targetChecksum);
            target.Metadata!.Size = 999;
            foreach (var mode in Enum.GetValues<ComparerErrorHandlingModes>())
                Assert.True(await new FileChecksumMetadataComparer(mode).AreEqual(new(source, target), default));
        }
        Assert.Equal(0, source.ReadCount);
    }

    [Theory]
    [InlineData("md5", 32)]
    [InlineData("sha1", 40)]
    public async Task valid_metadata_ignores_hex_case(string algorithm, int length)
    {
        var source = Source(new(algorithm, new string('a', length)));
        var target = Source(new(algorithm, new string('A', length)));
        Assert.True(await new FileChecksumMetadataComparer().AreEqual(new(source, target), default));
    }

    [Theory]
    [InlineData(false, "")]
    [InlineData(false, "123")]
    [InlineData(false, "partial")]
    [InlineData(true, "")]
    [InlineData(true, "123")]
    [InlineData(true, "partial")]
    public async Task omitted_checksum_preserves_existing_content_without_opening_either_file(
        bool omitMetadata, string existingContent)
    {
        var source = Source(null);
        if (omitMetadata)
            source.Metadata = null;
        var target = new TrackingLocalFile(RootedPath.Create(_root, "target.txt", _pathOptions));
        await File.WriteAllTextAsync(target.Path.GetFullPath(), existingContent);

        var result = await new ParallelSyncFilePairSyncer(1).CompareAndSyncFilePairs(
            [new SyncFilePair(source, target)], new LocalFileChecksumComparer(), null, null, default);

        Assert.Empty(result.UpdatedFiles);
        Assert.Single(result.IdenticalFiles);
        Assert.Equal(existingContent, await File.ReadAllTextAsync(target.Path.GetFullPath()));
        Assert.Equal(0, source.ReadCount);
        Assert.Equal(0, target.ReadCount);
        Assert.Equal(0, target.WriteCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task local_collection_creates_missing_file_with_omitted_checksum(bool omitMetadata)
    {
        var source = Source(null);
        if (omitMetadata)
            source.Metadata = null;
        var targetRoot = Path.Combine(_root, "target");
        Directory.CreateDirectory(targetRoot);
        var syncer = new LocalSyncer(targetRoot, _pathOptions, new ParallelSyncFilePairSyncer(1));
        var options = new SyncerOptions
        {
            Rules = [new(SyncAction.FullSync, SyncCondition.Always, "**", new LocalFileChecksumComparer())]
        };

        var result = await syncer.CompareAndSyncFiles([source], options);

        Assert.Single(result.AddedFiles);
        Assert.Single(result.UpdatedFilePairs);
        Assert.Empty(result.IdenticalFilePairs);
        Assert.Equal("source content", await File.ReadAllTextAsync(Path.Combine(targetRoot, "source.txt")));
        Assert.Equal(1, source.ReadCount);
    }

    [Fact]
    public async Task metadata_collection_comparison_classifies_missing_targets_before_checksum_comparison()
    {
        var source = Source(null);
        var syncer = new SyncFileCollectionSyncer(new ParallelSyncFilePairSyncer(1), _pathOptions);
        var options = new SyncerOptions
        {
            Rules = [new(SyncAction.FullSync, SyncCondition.Always, "**", new FileChecksumMetadataComparer())]
        };

        var missing = await syncer.CompareFiles([source], [], options);
        Assert.Single(missing.AddedFiles);
        Assert.Empty(missing.IdenticalFilePairs);

        // 메타데이터만 비교하므로 호출자가 제공한 대상 목록을 기준으로 존재 여부를 판단한다.
        var target = new VirtualSyncFile(source.Path);
        var existing = await syncer.CompareFiles([source], [target], options);
        Assert.Empty(existing.AddedFiles);
        Assert.Single(existing.IdenticalFilePairs);
    }

    private TrackingLocalFile Source(SyncFileChecksum? checksum)
    {
        var path = RootedPath.Create(_root, "source.txt", _pathOptions);
        File.WriteAllText(path.GetFullPath(), "source content");
        return new TrackingLocalFile(path)
        {
            Metadata = new() { Checksum = checksum, Size = 14 }
        };
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class TrackingLocalFile(RootedPath path) : LocalSyncFile(path)
    {
        public int ReadCount;
        public int WriteCount;

        public override ValueTask<Stream> OpenReadStream(CancellationToken cancellationToken)
        {
            ReadCount++;
            return base.OpenReadStream(cancellationToken);
        }

        public override ValueTask<Stream> OpenWriteStream(CancellationToken cancellationToken)
        {
            WriteCount++;
            return base.OpenWriteStream(cancellationToken);
        }
    }
}
