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
}