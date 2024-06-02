using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClientTest.FileComparers;

public class FileSizeMetadataComparerTests
{
    [Fact]
    public async Task compare_same_size()
    {
        // Given
        var comparer = new FileSizeMetadataComparer();
    
        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1111,
                Checksum = new SyncFileChecksum("md5", "abc")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1111,
                Checksum = new SyncFileChecksum("md5", "123")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.True(result);
    }

    [Fact]
    public async Task compare_different_size()
    {
        // Given
        var comparer = new FileSizeMetadataComparer();

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
        Assert.False(result);
    }

    [Fact]
    public async Task cannot_compare_negative_size()
    {
        // Given
        var comparer = new FileSizeMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 0,
                Checksum = new SyncFileChecksum("md5", "checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = -1,
                Checksum = new SyncFileChecksum("md5", "checksum")
            }
        };
        var exception = await Assert.ThrowsAsync<FileComparerException>(async () => 
            await comparer.AreEqual(new SyncFilePair(file1, file2), default));
    }

    [Fact]
    public async Task compare_zero_size()
    {
        // Given
        var comparer = new FileSizeMetadataComparer();

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 0,
                Checksum = new SyncFileChecksum("md5", "checksum")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 0,
                Checksum = new SyncFileChecksum("md5", "checksum")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.True(result);
    }
}