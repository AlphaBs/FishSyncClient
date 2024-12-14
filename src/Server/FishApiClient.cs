using FishSyncClient.Files;
using FishSyncClient.Server.BucketSyncActions;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;

namespace FishSyncClient.Server;

public class FishApiClient
{
    private readonly HttpClient _httpClient;
    private readonly string _host;

    public FishApiClient(string host, HttpClient httpClient) => 
        (_host, _httpClient) = 
        (host, httpClient);

    public string? ApiKey { get; set; }

    public async Task<IReadOnlyList<string>> ListBuckets(CancellationToken cancellationToken = default)
    {
        using var reqMessage = new HttpRequestMessage
        {
            RequestUri = new Uri(_host + "/buckets"),
            Method = HttpMethod.Get,
        };
        using var resStream = await request(reqMessage, cancellationToken);
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
        using var reqMessage = new HttpRequestMessage
        {
            RequestUri = new Uri($"{_host}/buckets/common/{id}"),
            Method = HttpMethod.Get,
        };
        using var resStream = await request(reqMessage, cancellationToken);
        var json = await JsonSerializer.DeserializeAsync<FishBucket>(resStream, cancellationToken: cancellationToken);
        return json ?? throw new FormatException();
    }

    public async Task<FishBucketFiles> GetBucketFiles(string id, CancellationToken cancellationToken = default)
    {
        using var reqMessage = new HttpRequestMessage
        {
            RequestUri = new Uri($"{_host}/buckets/common/{id}/files"),
            Method = HttpMethod.Get,
        };
        using var resStream = await request(reqMessage, cancellationToken);
        var json = await JsonSerializer.DeserializeAsync<FishBucketFiles>(resStream, cancellationToken: cancellationToken);
        return json ?? throw new FormatException();
    }

    public async Task<BucketSyncResult> Sync(string id, IEnumerable<SyncFile> files, CancellationToken cancellationToken)
    {
        var bucketSyncFiles = files.Select(file => new BucketSyncFile
        {
            Path = file.Path.SubPath,
            Checksum = file.Metadata?.Checksum?.ChecksumHexString,
            Size = file.Metadata?.Size ?? 0,
        });
        return await Sync(id, bucketSyncFiles, cancellationToken);
    }

    public async Task<BucketSyncResult> Sync(
        string id, 
        IEnumerable<BucketSyncFile> files, 
        CancellationToken cancellationToken = default)
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
        reqMessage.Headers.Add("Authorization", $"Bearer {ApiKey}");
        using var resStream = await request(reqMessage, cancellationToken);
        return await JsonSerializer.DeserializeAsync<BucketSyncResult>(resStream, cancellationToken: cancellationToken) ??
            throw new FormatException();
    }

    public async Task<BucketSyncResult> Sync(
        string id, 
        ISyncFileCollection sources,
        IBucketSyncActionCollectionHandler actionHandler, 
        CancellationToken cancellationToken = default)
    {
        int iterationCount = 0;
        BucketSyncResult result;
        while (true)
        {
            result = await Sync(id, sources, cancellationToken);
            if (result.IsSuccess)
                break;
            await actionHandler.Handle(sources, result.Actions, cancellationToken);

            iterationCount++;
            if (iterationCount > 10)
                break;
        }

        return result;
    }

    public async Task<string> Login(
        string username, 
        string password, 
        CancellationToken cancellationToken = default, 
        bool setApiKey = true)
    {
        var reqJson = JsonSerializer.Serialize(new { username, password });
        using var reqContent = new StringContent(reqJson);
        reqContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var reqMessage = new HttpRequestMessage
        {
            RequestUri = new Uri($"{_host}/auth/login"),
            Method = HttpMethod.Post,
            Content = reqContent
        };
        using var resStream = await request(reqMessage, cancellationToken);
        using var resJson = await JsonDocument.ParseAsync(resStream);
        var token = resJson.RootElement.GetProperty("token").GetString() ?? "";
        if (setApiKey)
            ApiKey = token;
        return token;
    }

    private async Task<Stream> request(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        var res = await _httpClient.SendAsync(message, cancellationToken);
        var resStream = await res.Content.ReadAsStreamAsync();
        if (res.IsSuccessStatusCode)
        {
            return resStream;
        }
        else
        {
            var exception = await parseErrorResponse(resStream, (int)res.StatusCode);
            resStream.Dispose();
            throw exception;
        }
    }

    private async Task<Exception> parseErrorResponse(Stream stream, int status)
    {
        try
        {
            var json = await JsonSerializer.DeserializeAsync<ProblemDetails>(stream) 
                ?? throw new FormatException();
            return new FishApiException(json);
        }
        catch (FormatException)
        {
            using var sr = new StreamReader(stream);
            var message = sr.ReadToEnd();
            return new FishApiException(message, status);
        }
    }
}