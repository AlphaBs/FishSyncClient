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
            source: CreatePaths(new []{"file1", "file2", "file3", "file4"}), 
            target: CreatePaths(new []{"file3", "file4", "file5", "file6"}));

        // Then
        var expected = CreatePaths(new []{"file3", "file4"});
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
        var result = sut.Sync(new []{sourceInstance}, new[]{targetInstance});

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
            source: CreatePaths(new []{"file1", "file2", "file3", "file4"}), 
            target: CreatePaths(new []{"file3", "file4", "file5", "file6"}));

        // Then
        var expected = CreatePaths(new []{"file1", "file2"});
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
        var result = sut.Sync(new []{sourceInstance}, new[]{targetInstance});

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
            source: CreatePaths(new []{"file1", "file2", "file3", "file4"}), 
            target: CreatePaths(new []{"file3", "file4", "file5", "file6"}));

        // Then
        var expected = CreatePaths(new []{"file5", "file6"});
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
        var result = sut.Sync(new []{sourceInstance}, new[]{targetInstance});

        // Then
        Assert.True(ReferenceEquals(targetInstance, result.DeletedPaths[0]));
        Assert.False(ReferenceEquals(sourceInstance, result.DeletedPaths[0]));
    }

    private static FishPath[] CreatePaths(IEnumerable<string> paths)
    {
        var list = new List<FishPath>();
        foreach (var path in paths)
        {
            list.Add(new FishPath(RootedPath.Create("root", path, pathOptions)));
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