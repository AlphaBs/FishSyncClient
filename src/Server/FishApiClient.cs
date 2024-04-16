using FishSyncClient.Files;
using System.Net.Http.Headers;
using System.Text.Json;

namespace FishSyncClient.Server;

public class FishApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _host;

    public FishApiClient(string host, HttpClient httpClient) => 
        (_host, _httpClient) = (host, httpClient);

    public async Task<IReadOnlyList<string>> ListBuckets(CancellationToken cancellationToken = default)
    {
        using var res = await _httpClient.GetAsync(_host + "/buckets", cancellationToken);
        res.EnsureSuccessStatusCode();
        using var resStream = await res.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(resStream, cancellationToken: cancellationToken);
        
        try
        {
            return json.RootElement
                .GetProperty("buckets")
                .EnumerateArray()
                .Select(elem => elem.GetString())
                .Where(elem => !string.IsNullOrEmpty(elem))
                .ToList()!;
        }
        catch (KeyNotFoundException)
        {
            throw new FormatException();
        }
        catch (InvalidOperationException)
        {
            throw new FormatException();
        }
    }

    public async Task<FishBucket> GetBucket(string id, CancellationToken cancellationToken = default)
    {
        using var res = await _httpClient.GetAsync($"{_host}/buckets/common/{id}", cancellationToken);
        res.EnsureSuccessStatusCode();
        using var resStream = await res.Content.ReadAsStreamAsync();
        var json = await JsonSerializer.DeserializeAsync<FishBucket>(resStream, cancellationToken: cancellationToken);
        return json ?? throw new FormatException();
    }

    public async Task<FishBucketFiles> GetBucketFiles(string id, CancellationToken cancellationToken = default)
    {
        using var res = await _httpClient.GetAsync($"{_host}/buckets/common/{id}/files", cancellationToken);
        res.EnsureSuccessStatusCode();
        using var resStream = await res.Content.ReadAsStreamAsync();
        var json = await JsonSerializer.DeserializeAsync<FishBucketFiles>(resStream, cancellationToken: cancellationToken);
        return json ?? throw new FormatException();
    }

    public async Task<BucketSyncResult> Sync(string id, IEnumerable<SyncFile> files)
    {
        var bucketSyncFiles = files.Select(file => new BucketSyncFile
        {
            Path = file.Path.SubPath,
            Checksum = file.Metadata?.Checksum,
            Size = file.Metadata?.Size ?? 0,
        });
        return await Sync(id, bucketSyncFiles);
    }

    public async Task<BucketSyncResult> Sync(string id, IEnumerable<BucketSyncFile> files)
    {
        var json = JsonSerializer.Serialize(new { files });
        using var reqContent = new StringContent(json);
        reqContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var reqMessage = new HttpRequestMessage
        {
            RequestUri = new Uri($"{_host}/buckets/common/{id}/sync"),
            Method = HttpMethod.Post,
            Content = reqContent
        };
        using var res = await _httpClient.SendAsync(reqMessage);
        var resStr = await res.Content.ReadAsStringAsync();

        res.EnsureSuccessStatusCode();
        using var resStream = await res.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<BucketSyncResult>(resStream) ?? 
            throw new FormatException();
    }
}