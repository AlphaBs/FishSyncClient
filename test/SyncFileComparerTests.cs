using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.PathMatchers;
using FishSyncClient.Syncer;
using Moq;

namespace FishSyncClientTest;

public class SyncFileComparerTests : SyncerTestBase
{
    [Fact]
    public async Task added_files_are_not_matched()
    {
        // Given
        var sut = CreateSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.CompareFiles(
            CreateSourcePaths("file1", "file2", "file222", "file34", "files/a/b/c"),
            CreateTargetPaths("files/a/b/c", "file5"),
            mockComparer.Object,
            new SyncerOptions
            {
                TargetPathMatcher = new GlobPathMatcher("file2*")
            });

        // Then
        // TargetPathMatcher 는 Source 에 추가된 파일들에 대해서 matching 을 수행하지 않음
        var expected = CreateSourcePaths("file1", "file2", "file222", "file34"); 
        var actual = result.AddedFiles.ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public async Task match_updated_files()
    {
        // Given
        var sut = CreateSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.CompareFiles(
            CreateSourcePaths("file1", "file2", "file222", "file34", "files/a/b/c"),
            CreateTargetPaths("file2", "file222", "file34", "files/a/b/c", "file5"),
            mockComparer.Object,
            new SyncerOptions
            {
                TargetPathMatcher = new GlobPathMatcher("file2*")
            });

        // Then
        var expected = CreateSourcePaths("file2", "file222");
        var actual = result.UpdatedFilePairs.Select(pair => pair.Source).ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public async Task match_deleted_files()
    {
        // Given
        var sut = CreateSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.CompareFiles(
            CreateSourcePaths("file1", "files/a/b/c"),
            CreateTargetPaths("file2", "file222", "file34", "files/a/b/c"),
            mockComparer.Object,
            new SyncerOptions
            {
                TargetPathMatcher = new GlobPathMatcher("file2*")
            });

        // Then
        var expected = CreateTargetPaths("file2", "file222");
        var actual = result.DeletedFiles.ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    public static SyncFileCollectionSyncer CreateSyncer()
    {
        var pathOptions = new PathOptions
        {
            CaseInsensitive = false
        };
        var syncer = new ParallelSyncFilePairSyncer(1);
        return new SyncFileCollectionSyncer(syncer, pathOptions);
    }
}