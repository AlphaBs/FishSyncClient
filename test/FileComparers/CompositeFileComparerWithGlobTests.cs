using FishSyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;

namespace FishSyncClientTest.FileComparers;

public class CompositeFileComparerWithGlobTests
{
    [Fact]
    public async Task use_first_matched_comparer()
    {
        // Given
        var comparer = new CompositeFileComparerWithGlob();
        comparer.Add("compare_only_size/**", new FileSizeMetadataComparer());
        comparer.Add("**", new FileChecksumMetadataComparer());

        // When
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("compare_only_size/file", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1234,
                Checksum = new SyncFileChecksum("md5", "A")
            }
        };
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("compare_only_size/file", new()))
        {
            Metadata = new SyncFileMetadata
            {
                Size = 1234,
                Checksum = new SyncFileChecksum("md5", "B")
            }
        };
        var result = await comparer.AreEqual(new SyncFilePair(file1, file2), default);

        // Then
        Assert.True(result);
    }

    [Fact]
    public async Task throw_no_matching_pattern()
    {
        // Given
        var comparer = new CompositeFileComparerWithGlob();
        
        // When Then
        var file1 = new VirtualSyncFile(RootedPath.FromSubPath("file1", new()));
        var file2 = new VirtualSyncFile(RootedPath.FromSubPath("file2", new()));
        var exception = await Assert.ThrowsAsync<FileComparerException>(async () => 
            await comparer.AreEqual(new SyncFilePair(file1, file2), default));
    }
}