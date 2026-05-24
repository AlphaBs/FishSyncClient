using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClientTest.FileComparers;

public class FileChecksumMetadataComparerTests
{
    [Fact]
    public async Task compare_same_checksum()
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1111,
                Checksum = new SyncFileChecksum("md5", "checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 2222,
                Checksum = new SyncFileChecksum("md5", "checksum")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.True(result);
    }

    [Fact]
    public async Task compare_different_checksum()
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1111,
                Checksum = new SyncFileChecksum("md5", "11111111111")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 2222,
                Checksum = new SyncFileChecksum("md5", "2222222222")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.False(result);
    }

    [Fact]
    public async Task cannot_compare_when_checksum_algorithms_are_different()
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1111,
                Checksum = new SyncFileChecksum("__algName__", "checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 2222,
                Checksum = new SyncFileChecksum("### AlgorithmName ###", "checksum")
            }
        };

        // Then
        var exception = await Assert.ThrowsAsync<FileComparerException>(async () =>  
            await comparer.AreEqual(new SyncFilePair(file1, file2), default));
    }

    [Theory]
    [InlineData(ComparerErrorHandlingModes.ReturnEqual, true)]
    [InlineData(ComparerErrorHandlingModes.ReturnNotEqual, false)]
    public async Task use_error_mode_when_checksum_algorithms_are_different(
        ComparerErrorHandlingModes mode,
        bool expected)
    {
        // Given
        var comparer = new FileChecksumMetadataComparer(mode);

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum("md5", "checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum("sha1", "checksum")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task return_equal_when_source_checksum_is_missing()
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1111
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 2222,
                Checksum = new SyncFileChecksum("md5", "any-checksum")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.True(result);
    }

    [Fact]
    public async Task return_equal_when_source_metadata_is_missing()
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()));
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum("md5", "any-checksum")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.True(result);
    }

    [Theory]
    [InlineData("", "checksum")]
    [InlineData("md5", "")]
    public async Task return_equal_when_source_checksum_has_empty_value(string algorithmName, string checksum)
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum(algorithmName, checksum)
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum("md5", "any-checksum")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.True(result);
    }

    [Fact]
    public async Task cannot_compare_when_source_checksum_exists_and_target_checksum_is_missing()
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1111,
                Checksum = new SyncFileChecksum("md5", "any-checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 2222
            }
        };
        // Then
        var exception = await Assert.ThrowsAsync<FileComparerException>(async () =>
            await comparer.AreEqual(new SyncFilePair(file1, file2), default));
    }

    [Theory]
    [InlineData("", "checksum")]
    [InlineData("md5", "")]
    public async Task cannot_compare_when_target_checksum_has_empty_value(string algorithmName, string checksum)
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum("md5", "any-checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum(algorithmName, checksum)
            }
        };

        // Then
        var exception = await Assert.ThrowsAsync<FileComparerException>(async () =>
            await comparer.AreEqual(new SyncFilePair(file1, file2), default));
    }

    [Theory]
    [InlineData(ComparerErrorHandlingModes.ReturnEqual, true)]
    [InlineData(ComparerErrorHandlingModes.ReturnNotEqual, false)]
    public async Task use_error_mode_when_source_checksum_exists_and_target_checksum_is_missing(
        ComparerErrorHandlingModes mode,
        bool expected)
    {
        // Given
        var comparer = new FileChecksumMetadataComparer(mode);

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum("md5", "any-checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata()
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task cannot_compare_when_source_checksum_exists_and_target_metadata_is_missing()
    {
        // Given
        var comparer = new FileChecksumMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Checksum = new SyncFileChecksum("md5", "any-checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()));

        // Then
        var exception = await Assert.ThrowsAsync<FileComparerException>(async () =>
            await comparer.AreEqual(new SyncFilePair(file1, file2), default));
    }
}
