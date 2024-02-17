using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using Moq;

namespace FishSyncClientTest;

public class FileChecksumComparerTests
{
    private readonly static IFileComparer FirstComparerMock = new Mock<IFileComparer>().Object;
    private readonly static IFileComparer SecondComparerMock = new Mock<IFileComparer>().Object;
    private readonly static IFileComparer DefaultComparerMock = new Mock<IFileComparer>().Object;

    private static PathOptions pathOptions = new PathOptions();

    [Fact]
    public void create_common_algorithm()
    {
        // Given
        var sut = CreateComparer();
    
        // When
        var comparer = sut.GetComparer(new FishPathPair(
            CreateSubFileWithChecksumAlgorithm("1"),
            CreateSubFileWithChecksumAlgorithm("1")
        ));
    
        // Then
        Assert.Equal(FirstComparerMock, comparer);
    }

    [Fact]
    public void cannot_create_comparer_when_no_common_algorithm_and_no_roots()
    {
        // Given
        var sut = CreateComparer();

        // When
        var comparer = sut.GetComparer(new FishPathPair(
            CreateSubFileWithChecksumAlgorithm("1"),
            CreateSubFileWithChecksumAlgorithm("2")
        ));

        // Then
        Assert.Null(comparer);
    }

    [Fact]
    public void choose_algorithm_of_non_rooted_path()
    {
        // Given
        var sut = CreateComparer();
    
        // When
        var comparer = sut.GetComparer(new FishPathPair(
            CreateRootedFileWithChecksumAlgorithm("1"),
            CreateSubFileWithChecksumAlgorithm("2")
        ));

        // Then
        Assert.Equal(SecondComparerMock, comparer);
    }

    [Fact]
    public void choose_existing_comparer()
    {
        // Given
        var sut = CreateComparer();
    
        // When
        var comparer = sut.GetComparer(new FishPathPair(
            CreateRootedFileWithChecksumAlgorithm("1"),
            CreateRootedFileWithChecksumAlgorithm("???")
        ));
    
        // Then
        Assert.Equal(FirstComparerMock, comparer);
    }

    [Fact]
    public void choose_specified_algorithm()
    {
        // Given
        var sut = CreateComparer();
    
        // When
        var comparer = sut.GetComparer(new FishPathPair(
            CreateSubFileWithChecksumAlgorithm("1"),
            CreateRootedFile()
        ));
    
        // Then
        Assert.Equal(FirstComparerMock, comparer);
    }

    [Fact]
    public void use_default_comparer_when_no_algorithm_is_specified()
    {
        // Given
        var sut = CreateComparer();

        // When
        var comparer = sut.GetComparer(new FishPathPair(
            CreateRootedFile(),
            CreateRootedFile()
        ));
    
        // Then
        Assert.Equal(DefaultComparerMock, comparer);
    }

    [Fact]
    public void cannot_create_comparer_when_cannot_find_any_algorithm()
    {
        // Given
        var sut = CreateComparer();

        // When
        var comparer = sut.GetComparer(new FishPathPair(
            CreateSubFileWithChecksumAlgorithm("???"),
            CreateSubFileWithChecksumAlgorithm("??????")
        ));
    
        // Then
        Assert.Null(comparer);
    }

    private static FileChecksumComparer CreateComparer()
    {
        var comparer = new FileChecksumComparer();
        comparer.AddAlgorithm("1", FirstComparerMock);
        comparer.AddAlgorithm("2", SecondComparerMock);
        comparer.DefaultComparer = DefaultComparerMock;
        return comparer;
    }

    private static FishFileMetadata CreateRootedFileWithChecksumAlgorithm(string alg)
    {
        return new FishFileMetadata(
            path: RootedPath.Create("root", "file", pathOptions),
            size: 0,
            checksum: alg,
            checksumAlgorithm: alg);
    }

    private static FishFileMetadata CreateSubFileWithChecksumAlgorithm(string alg)
    {
        return new FishFileMetadata(
            path: RootedPath.FromSubPath("file", pathOptions),
            size: 0,
            checksum: alg,
            checksumAlgorithm: alg);
    }

    private static FishPath CreateRootedFile()
    {
        return new FishPath(RootedPath.Create("root", "file", pathOptions));
    }
}