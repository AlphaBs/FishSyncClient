using FishSyncClient;
using FishSyncClient.Syncer;

namespace FishSyncClientTest;

public class FishPathSyncTests
{
    private static PathOptions pathOptions = new();

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
    public void duplicated_paths_keep_source_reference()
    {
        // Given
        var sourceInstance = new SyncFile(RootedPath.Create("root", "duplicated", pathOptions));
        var targetInstance = new SyncFile(RootedPath.Create("root", "duplicated", pathOptions));
        var sut = new FishPathSyncer();

        // When
        var result = sut.Sync(new[] { sourceInstance }, new[] { targetInstance });

        // Then
        Assert.True(ReferenceEquals(sourceInstance, result.DuplicatedPaths[0].Source));
        Assert.False(ReferenceEquals(targetInstance, result.DuplicatedPaths[0].Source));
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
    public void added_paths_keep_source_reference()
    {
        // Given
        var sourceInstance = new SyncFile(RootedPath.Create("root", "added", pathOptions));
        var targetInstance = new SyncFile(RootedPath.Create("root", "file", pathOptions));
        var sut = new FishPathSyncer();

        // When
        var result = sut.Sync(new[] { sourceInstance }, new[] { targetInstance });

        // Then
        Assert.True(ReferenceEquals(sourceInstance, result.AddedPaths[0]));
        Assert.False(ReferenceEquals(targetInstance, result.AddedPaths[0]));
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

    [Fact]
    public void deleted_paths_keep_target_reference()
    {
        // Given
        var sourceInstance = new SyncFile(RootedPath.Create("root", "file", pathOptions));
        var targetInstance = new SyncFile(RootedPath.Create("root", "deleted", pathOptions));
        var sut = new FishPathSyncer();

        // When
        var result = sut.Sync(new[] { sourceInstance }, new[] { targetInstance });

        // Then
        Assert.True(ReferenceEquals(targetInstance, result.DeletedPaths[0]));
        Assert.False(ReferenceEquals(sourceInstance, result.DeletedPaths[0]));
    }

    [Fact]
    public void exclude_duplicated_files()
    {
        // Given
        var sut = new FishPathSyncer(
            new [] { "file2", "file3*", "files/**" });

        // When
        var result = sut.Sync(
            CreateSourcePaths("file1", "file2", "file222", "file34", "files/a/b/c"),
            CreateTargetPaths(         "file2", "file222", "file34", "files/a/b/c", "file5"));

        // Then
        var expected = CreateSourcePaths("file222");
        var actual = result.DuplicatedPaths.Select(st => st.Source).ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public void exclude_deleted_files()
    {
        // Given
        var sut = new FishPathSyncer(
            new [] { "file2", "file3*", "files/**" });

        // When
        var result = sut.Sync(
            source: CreateSourcePaths("file1"),
            target: CreateTargetPaths("file2", "file222", "file34", "files/a/b/c"));

        // Then
        var expected = CreateTargetPaths("file222");
        AssertEqualPathCollection(expected, result.DeletedPaths);
    }

    private static SyncFile[] CreateSourcePaths(params string[] paths)
    {
        return CreatePathsFromRoot("source", paths);
    }

    private static SyncFile[] CreateTargetPaths(params string[] paths)
    {
        return CreatePathsFromRoot("target", paths);
    }

    private static SyncFile[] CreatePathsFromRoot(string root, params string[] paths)
    {
        var list = new List<SyncFile>();
        foreach (var path in paths)
        {
            list.Add(new SyncFile(RootedPath.Create(root, path, pathOptions)));
        }
        return list.ToArray();
    }

    private static void AssertEqualPathCollection(SyncFile[] expected, SyncFile[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        foreach (var test in expected)
        {
            Assert.Contains(test, actual);
        }
    }
}