using FishSyncClient.Syncer;

namespace FishSyncClientTest;

public class FishPathSyncTests : FishFileTestBase
{
    [Fact]
    public void find_duplicated_paths()
    {
        // Given
        var sut = new FishPathSyncer();

        // When
        var result = sut.Sync(
            source: CreateSourcePaths("file1", "file2", "file3", "file4"),
            target: CreateTargetPaths("file3", "file4", "file5", "file6"));

        // Then
        var expected = CreateSourcePaths("file3", "file4");
        var actual = result.DuplicatedPaths.Select(st => st.Source).ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public void find_added_paths()
    {
        // Given
        var sut = new FishPathSyncer();

        // When
        var result = sut.Sync(
            source: CreateSourcePaths("file1", "file2", "file3", "file4"),
            target: CreateTargetPaths("file3", "file4", "file5", "file6"));

        // Then
        var expected = CreateSourcePaths("file1", "file2");
        AssertEqualPathCollection(expected, result.AddedPaths);
    }

    [Fact]
    public void find_deleted_paths()
    {
        // Given
        var sut = new FishPathSyncer();

        // When
        var result = sut.Sync(
            source: CreateSourcePaths("file1", "file2", "file3", "file4"),
            target: CreateTargetPaths("file3", "file4", "file5", "file6"));

        // Then
        var expected = CreateTargetPaths("file5", "file6");
        AssertEqualPathCollection(expected, result.DeletedPaths);
    }
}