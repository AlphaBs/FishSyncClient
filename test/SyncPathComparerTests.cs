using FishSyncClient;
using FishSyncClient.Syncer;

namespace FishSyncClientTest;

public class SyncPathComparerTests : SyncerTestBase
{
    [Fact]
    public void find_duplicated_paths()
    {
        // Given
        var sut = new SyncPathComparer();
        var pathOptions = new PathOptions
        {
            CaseInsensitive = false
        };

        // When
        var result = sut.ComparePaths(
            source: CreateSourcePaths("file1", "file2", "file3", "file4"),
            target: CreateTargetPaths("file3", "file4", "file5", "file6"),
            pathOptions);

        // Then
        var expected = CreateSourcePaths("file3", "file4");
        var actual = result.DuplicatedFiles.Select(st => st.Source).ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public void find_duplicated_paths_with_case_insensitive()
    {
        // Given
        var sut = new SyncPathComparer();
        var pathOptions = new PathOptions
        {
            CaseInsensitive = true
        };

        // When
        var result = sut.ComparePaths(
            source: CreateSourcePaths("file1", "file2", "fiLe3", "FILe4"),
            target: CreateTargetPaths("FILE3", "file4", "file5", "file6"),
            pathOptions);

        // Then
        var expected = CreateSourcePaths("fiLe3", "FILe4");
        var actual = result.DuplicatedFiles.Select(st => st.Source).ToArray();
        AssertEqualPathCollection(expected, actual);
    }

    [Fact]
    public void find_added_paths()
    {
        // Given
        var sut = new SyncPathComparer();
        var pathOptions = new PathOptions
        {
            CaseInsensitive = false
        };

        // When
        var result = sut.ComparePaths(
            source: CreateSourcePaths("FILE1", "file2", "file3", "file4"),
            target: CreateTargetPaths("FILE3", "file4", "file5", "file6"),
            pathOptions);

        // Then
        var expected = CreateSourcePaths("FILE1", "file2", "file3");
        AssertEqualPathCollection(expected, result.AddedFiles);
    }

    [Fact]
    public void find_added_paths_with_case_insensitive()
    {
        // Given
        var sut = new SyncPathComparer();
        var pathOptions = new PathOptions
        {
            CaseInsensitive = true
        };

        // When
        var result = sut.ComparePaths(
            source: CreateSourcePaths("FILE1", "file2", "File3", "filE4"),
            target: CreateTargetPaths("fIle3", "fiLe4", "file5", "file6"),
            pathOptions);

        // Then
        var expected = CreateSourcePaths("FILE1", "file2");
        AssertEqualPathCollection(expected, result.AddedFiles);
    }

    [Fact]
    public void find_deleted_paths()
    {
        // Given
        var sut = new SyncPathComparer();
        var pathOptions = new PathOptions
        {
            CaseInsensitive = false
        };

        // When
        var result = sut.ComparePaths(
            source: CreateSourcePaths("file1", "file2", "file3", "file4"),
            target: CreateTargetPaths("file3", "FILE4", "file5", "file6"),
            pathOptions);

        // Then
        var expected = CreateTargetPaths("FILE4", "file5", "file6");
        AssertEqualPathCollection(expected, result.DeletedFiles);
    }

    [Fact]
    public void find_deleted_paths_with_case_insensitive()
    {
        // Given
        var sut = new SyncPathComparer();
        var pathOptions = new PathOptions
        {
            CaseInsensitive = true
        };

        // When
        var result = sut.ComparePaths(
            source: CreateSourcePaths("file1", "file2", "file3", "file4"),
            target: CreateTargetPaths("file3", "file4", "file5", "file6"),
            pathOptions);

        // Then
        var expected = CreateTargetPaths("file5", "file6");
        AssertEqualPathCollection(expected, result.DeletedFiles);
    }

    [Fact]
    public void find_composite_paths()
    {
        // Given
        var sut = new SyncPathComparer();
        var pathOptions = new PathOptions
        {
            CaseInsensitive = false
        };

        // When
        var result = sut.ComparePaths(
            source: CreateSourcePaths("file1", "file2", "FILE3", "file4"),
            target: CreateTargetPaths("file3", "file4", "file5", "file6"),
            pathOptions);

        // Then
        var added = CreateSourcePaths("file1", "file2", "FILE3");
        var duplicated = CreateSourcePaths("file4");
        var deleted = CreateTargetPaths("file3", "file5", "file6");

        AssertEqualPathCollection(added, result.AddedFiles);
        AssertEqualPathCollection(duplicated, result.DuplicatedFiles.Select(pair => pair.Source));
        AssertEqualPathCollection(deleted, result.DeletedFiles);
    }

    [Fact]
    public void find_composite_paths_with_case_insensitive()
    {
        // Given
        var sut = new SyncPathComparer();
        var pathOptions = new PathOptions
        {
            CaseInsensitive = true
        };

        // When
        var result = sut.ComparePaths(
            source: CreateSourcePaths("file1", "file2", "FILE3", "file4"),
            target: CreateTargetPaths("file3", "FILE4", "file5", "file6"),
            pathOptions);

        // Then
        var added = CreateSourcePaths("file1", "file2");
        var duplicated = CreateSourcePaths("FILE3", "file4");
        var deleted = CreateTargetPaths("file5", "file6");

        AssertEqualPathCollection(added, result.AddedFiles);
        AssertEqualPathCollection(duplicated, result.DuplicatedFiles.Select(pair => pair.Source));
        AssertEqualPathCollection(deleted, result.DeletedFiles);
    }
}