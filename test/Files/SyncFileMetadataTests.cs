using FishSyncClient;
using FishSyncClient.Files;

namespace FishSyncClientTest.Files;

public class SyncFileMetadataTests
{
    [Fact]
    public void metadata_compares_by_value_and_with_keeps_the_original_unchanged()
    {
        var original = new SyncFileMetadata
        {
            Size = 3,
            Checksum = new("md5", "202cb962ac59075b964b07152d234b70")
        };
        var sameValue = new SyncFileMetadata
        {
            Size = 3,
            Checksum = new("md5", "202cb962ac59075b964b07152d234b70")
        };
        var changed = original with { Size = 8, Checksum = null };

        Assert.NotSame(original, sameValue);
        Assert.True(original == sameValue);
        Assert.Equal(original.GetHashCode(), sameValue.GetHashCode());
        Assert.Equal(3, original.Size);
        Assert.NotNull(original.Checksum);
        Assert.Equal(8, changed.Size);
        Assert.Null(changed.Checksum);
        Assert.NotEqual(original, changed);
    }

    [Fact]
    public void files_use_path_and_metadata_values_as_dictionary_keys()
    {
        var path = RootedPath.FromSubPath("file.bin", new PathOptions());
        var first = new VirtualSyncFile(path) { Metadata = new() { Size = 3 } };
        var sameValue = new VirtualSyncFile(path) { Metadata = new() { Size = 3 } };
        var differentSize = new VirtualSyncFile(path) { Metadata = first.Metadata with { Size = 4 } };
        var differentChecksum = new VirtualSyncFile(path)
        {
            Metadata = first.Metadata with { Checksum = new("md5", "202cb962ac59075b964b07152d234b70") }
        };
        var differentPath = new VirtualSyncFile(RootedPath.FromSubPath("other.bin", new PathOptions()))
        {
            Metadata = first.Metadata
        };
        var keys = new Dictionary<SyncFile, string> { [first] = "found" };

        Assert.Equal(first, sameValue);
        Assert.Equal("found", keys[sameValue]);
        Assert.NotEqual(first, differentSize);
        Assert.NotEqual(first, differentChecksum);
        Assert.NotEqual(first, differentPath);
        Assert.NotEqual(first, new VirtualSyncFile(path));
        Assert.Equal(new VirtualSyncFile(path), new VirtualSyncFile(path));
    }

    [Fact]
    public void checksum_value_equality_preserves_hex_case()
    {
        var lower = new SyncFileMetadata { Checksum = new("md5", "202cb962ac59075b964b07152d234b70") };
        var upper = lower with { Checksum = new("md5", "202CB962AC59075B964B07152D234B70") };

        Assert.NotEqual(lower, upper);
    }
}
