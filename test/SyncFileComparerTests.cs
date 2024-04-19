using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Syncer;
using Moq;

namespace FishSyncClientTest;

public class SyncFileComparerTests : SyncerTestBase
{
    [Fact]
    public async Task exclude_files_which_match_exclude_patterns_from_updated_files()
    {
        // Given
        var sut = CreateSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.CompareFiles(
            CreateSourcePaths("file1", "file2", "file222", "file34", "files/a/b/c"),
            CreateTargetPaths(         "file2", "file222", "file34", "files/a/b/c", "file5"),
            mockComparer.Object,
            new SyncFileComparerOptions
            {
                Excludes = new [] { "file2", "file3*", "files/**" }
            });

        // Then
        var expected = CreateSourcePaths("file222");
        var actual = result.UpdatedFilePairs.Select(pair => pair.Source).ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public async Task exclude_files_which_does_not_match_include_patterns_from_updated_files()
    {
        // Given
        var sut = CreateSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.CompareFiles(
            CreateSourcePaths("file1", "file2", "file222", "file34", "files/a/b/c"),
            CreateTargetPaths(         "file2", "file222", "file34", "files/a/b/c", "file5"),
            mockComparer.Object,
            new SyncFileComparerOptions
            {
                Includes = new [] { "file2", "file3*", "files/**" }
            });

        // Then
        var expected = CreateSourcePaths("file2", "file34", "files/a/b/c");
        var actual = result.UpdatedFilePairs.Select(pair => pair.Source).ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public async Task exclude_files_which_match_exclude_patterns_from_deleted_files()
    {
        // Given
        var sut = CreateSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.CompareFiles(
            CreateSourcePaths("file1"),
            CreateTargetPaths("file2", "file222", "file34", "files/a/b/c"),
            mockComparer.Object,
            new SyncFileComparerOptions
            {
                Excludes = new [] { "file2", "file3*", "files/**" }
            });

        // Then
        var expected = CreateTargetPaths("file222");
        var actual = result.DeletedFiles.ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public async Task exclude_files_which_does_not_match_include_patterns_from_deleted_files()
    {
        // Given
        var sut = CreateSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.CompareFiles(
            CreateSourcePaths("file1"),
            CreateTargetPaths("file2", "file222", "file34", "files/a/b/c"),
            mockComparer.Object,
            new SyncFileComparerOptions
            {
                Includes = new [] { "file2", "file3*", "files/**" }
            });

        // Then
        var expected = CreateTargetPaths("file2", "file34", "files/a/b/c");
        var actual = result.DeletedFiles.ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    public static SyncFileComparer CreateSyncer()
    {
        return new SyncFileComparer(new SequentialSyncFilePairComparer());
    }
}