using FishSyncClient.Files;
using System.Text.Json.Serialization;

namespace FishSyncClient.Server.Alphabet;

public class UpdateFileCollection
{
    [JsonPropertyName("lastUpdate")]
    public DateTimeOffset LastUpdate { get; set; }

    [JsonPropertyName("hashAlgorithm")]
    public string? HashAlgorithm { get; set; }
    
    [JsonPropertyName("files")]
    public UpdateFile[]? Files { get; set; }

    public IEnumerable<ReadableHttpSyncFile> ToSyncFiles(HttpClient httpClient, PathOptions options)
    {
        if (Files == null)
            yield break;

        var checksumAlgorithm = string.IsNullOrEmpty(HashAlgorithm)
            ? "md5"
            : HashAlgorithm;

        foreach (var file in Files)
        {
            if (string.IsNullOrEmpty(file.Path))
                continue;

            yield return new ReadableHttpSyncFile(RootedPath.FromSubPath(file.Path, options), httpClient)
            {
                Metadata = new SyncFileMetadata
                {
                    Size = file.Size,
                    Checksum = file.Hash,
                    ChecksumAlgorithm = checksumAlgorithm,
                },
                Uploaded = DateTimeOffset.MinValue,
                Location = new Uri(file.Url)
            };
        }
    }
}