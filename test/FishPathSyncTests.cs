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
        AssertEqualPathCollection(expected, result.DuplicatedPaths);
    }

    [Fact]
    public void duplicated_paths_keep_source_reference()
    {
        // Given
        var sourceInstance = new FishPath(RootedPath.Create("root", "duplicated", pathOptions));
        var targetInstance = new FishPath(RootedPath.Create("root", "duplicated", pathOptions));
        var sut = new FishPathSyncer();

        // When
        var result = sut.Sync(new[] { sourceInstance }, new[] { targetInstance });

        // Then
        Assert.True(ReferenceEquals(sourceInstance, result.DuplicatedPaths[0]));
        Assert.False(ReferenceEquals(targetInstance, result.DuplicatedPaths[0]));
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
        var sourceInstance = new FishPath(RootedPath.Create("root", "added", pathOptions));
        var targetInstance = new FishPath(RootedPath.Create("root", "file", pathOptions));
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
        var sourceInstance = new FishPath(RootedPath.Create("root", "file", pathOptions));
        var targetInstance = new FishPath(RootedPath.Create("root", "deleted", pathOptions));
        var sut = new FishPathSyncer();

        // When
        var result = sut.Sync(new[] { sourceInstance }, new[] { targetInstance });

        // Then
        Assert.True(ReferenceEquals(targetInstance, result.DeletedPaths[0]));
        Assert.False(ReferenceEquals(sourceInstance, result.DeletedPaths[0]));
    }

    [Fact]
    public void black_duplicated_files()
    {
        // Given
        var sut = new FishPathSyncer(
            CreateSourcePaths("file2").Select(p => p.Path));

        // When
        var result = sut.Sync(
            source: CreateSourcePaths("file1", "file2", "file22"),
            target: CreateTargetPaths("file2", "file22", "file3"));

        // Then
        // a syncer must compare the full paths of the files. paths that start with a blacklisted file path are not always blacklisted files.
        var expected = CreateSourcePaths("file22");
        AssertEqualPathCollection(expected, result.DuplicatedPaths);
    }

    [Fact]
    public void black_duplicated_dirs()
    {
        // Given
        var sut = new FishPathSyncer(
            CreateSourcePaths("files/").Select(p => p.Path));

        // When
        var result = sut.Sync(
            source: CreateSourcePaths("file1", "files/file2/test", "file3"),
            target: CreateTargetPaths("files/file2/test", "file3", "files/file4"));

        // Then
        var expected = CreateSourcePaths("file3");
        AssertEqualPathCollection(expected, result.DuplicatedPaths);
    }

    [Fact]
    public void black_deleted_files()
    {
        // Given
        var sut = new FishPathSyncer(
            CreateSourcePaths("file3").Select(p => p.Path));

        // When
        var result = sut.Sync(
            source: CreateSourcePaths("file1", "file2"),
            target: CreateTargetPaths("file2", "file3", "file33"));

        // Then
        // a syncer must compare the full paths of the files. paths that start with a blacklisted file path are not always blacklisted files.
        var expected = CreateTargetPaths("file33");
        AssertEqualPathCollection(expected, result.DeletedPaths);
    }

    [Fact]
    public void black_deleted_dirs()
    {
        // Given
        var sut = new FishPathSyncer(
            CreateSourcePaths("files/").Select(p => p.Path));

        // When
        var result = sut.Sync(
            source: CreateSourcePaths("file1", "files/file2/test", "file3"),
            target: CreateTargetPaths("files/file2/test", "file3", "files/file4"));

        // Then
        Assert.Empty(result.DeletedPaths);
    }

    private static FishPath[] CreateSourcePaths(params string[] paths)
    {
        return CreatePathsFromRoot("source", paths);
    }

    private static FishPath[] CreateTargetPaths(params string[] paths)
    {
        return CreatePathsFromRoot("target", paths);
    }

    private static FishPath[] CreatePathsFromRoot(string root, params string[] paths)
    {
        var list = new List<FishPath>();
        foreach (var path in paths)
        {
            list.Add(new FishPath(RootedPath.Create(root, path, pathOptions)));
        }
        return list.ToArray();
    }

    private static void AssertEqualPathCollection(FishPath[] expected, FishPath[] actual)
    {
        Assert.Equal(expected.Length, actual.Length);
        foreach (var test in expected)
        {
            Assert.Contains(test, actual);
        }
    }
}