using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using Moq;

namespace FishSyncClientTest;

public class FishSyncerTests : FishFileTestBase
{
    [Fact]
    public async Task exclude_files_which_match_exclude_patterns_from_updated_files()
    {
        // Given
        var sut = new FishSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.Sync(
            CreateSourcePaths("file1", "file2", "file222", "file34", "files/a/b/c"),
            CreateTargetPaths(         "file2", "file222", "file34", "files/a/b/c", "file5"),
            mockComparer.Object,
            new SyncOptions
            {
                Excludes = new [] { "file2", "file3*", "files/**" }
            });

        // Then
        var expected = CreateSourcePaths("file1", "file222");
        var actual = result.UpdatedFiles.ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public async Task exclude_files_which_does_not_match_include_patterns_from_updated_files()
    {
        // Given
        var sut = new FishSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.Sync(
            CreateSourcePaths("file1", "file2", "file222", "file34", "files/a/b/c"),
            CreateTargetPaths(         "file2", "file222", "file34", "files/a/b/c", "file5"),
            mockComparer.Object,
            new SyncOptions
            {
                Includes = new [] { "file2", "file3*", "files/**" }
            });

        // Then
        var expected = CreateSourcePaths("file2", "file34", "files/a/b/c");
        var actual = result.UpdatedFiles.ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public async Task exclude_files_which_match_exclude_patterns_from_deleted_files()
    {
        // Given
        var sut = new FishSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.Sync(
            CreateSourcePaths("file1"),
            CreateTargetPaths("file2", "file222", "file34", "files/a/b/c"),
            mockComparer.Object,
            new SyncOptions
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
        var sut = new FishSyncer();
        var mockComparer = new Mock<IFileComparer>();
        mockComparer.Setup(comparer => comparer.AreEqual(It.IsAny<SyncFilePair>(), default))
            .Returns(new ValueTask<bool>(false));

        // When
        var result = await sut.Sync(
            CreateSourcePaths("file1"),
            CreateTargetPaths("file2", "file222", "file34", "files/a/b/c"),
            mockComparer.Object,
            new SyncOptions
            {
                Includes = new [] { "file2", "file3*", "files/**" }
            });

        // Then
        var expected = CreateTargetPaths("file2", "file34", "files/a/b/c");
        var actual = result.DeletedFiles.ToArray();
        AssertEqualPathCollection(expected, actual);
    }
}