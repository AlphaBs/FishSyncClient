using FishSyncClient.Files;
using System.Text.Json.Serialization;

namespace FishSyncClient.Server;

public class FishBucket
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset? LastUpdated { get; set; }

    [JsonPropertyName("limitations")]
    public FishBucketLimitations? Limitations { get; set; }

    [JsonPropertyName("files")]
    public FishBucketFile[]? Files { get; set; }
}

public class FishBucketFiles
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("lastUpdated")]
    public DateTimeOffset LastUpdated { get; set; }

    [JsonPropertyName("files")]
    public FishBucketFile[]? Files { get; set; }

    public IEnumerable<SyncFile> GetSyncFiles(HttpClient httpClient, PathOptions options)
    {
        if (Files == null)
            return [];

        return Files.Select(file => 
            new ReadableHttpSyncFile(RootedPath.FromSubPath(file.Path, options), httpClient)
            {
                Location = new Uri(file.Location),
                Uploaded = file.Metadata.LastUpdated,
                Metadata = new SyncFileMetadata()
                { 
                    Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.MD5, file.Metadata.Checksum),
                    Size = file.Metadata.Size,
                },
            });
    }
}

public class FishBucketLimitations
{
    public bool IsReadOnly { get; set; }
    public long MaxFileSize { get; set; }
    public long MaxNumberOfFiles { get; set; }
    public long MaxBucketSize { get; set; }
    public DateTimeOffset ExpiredAt { get; set; }
}

public record FishBucketFile(
    [property:JsonPropertyName("path")] string Path, 
    [property:JsonPropertyName("location")] string? Location,
    [property:JsonPropertyName("metadata")] FishFileMetadata Metadata);

public record FishFileMetadata(
    [property:JsonPropertyName("size")] long Size,
    [property:JsonPropertyName("lastUpdated")] DateTimeOffset LastUpdated,
    [property:JsonPropertyName("checksum")] string Checksum);