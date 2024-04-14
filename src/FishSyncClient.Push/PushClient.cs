using System.Net.Http.Headers;
using System.Text.Json;
using FishSyncClient.Common;

namespace FishSyncClient.Push;

public class PushClient
{
    private readonly HttpClient _httpClient;
    private readonly Uri _host;

    public PushClient(Uri host, HttpClient httpClient)
    {
        _host = host;
        _httpClient = httpClient;
    }

    public BucketSyncFile CreateSyncFile(RootedPath path)
    {
        var fileinfo = new FileInfo(path.GetFullPath());
        using var fs = File.OpenRead(fileinfo.FullName);
        var checksum = ChecksumAlgorithms.ComputeMD5(fs);

        return new BucketSyncFile
        {
            Path = path.SubPath,
            Size = fileinfo.Length,
            Checksum = checksum,
        };
    }

    public async ValueTask<BucketSyncResult> Push(
        string id, 
        IEnumerable<BucketSyncFile> files, 
        IBucketSyncActionCollectionHandler handler,
        CancellationToken cancellationToken)
    {
        int iterationCount = 0;
        while (true)
        {
            var syncResult = await Push(id, files);
            if (syncResult.IsSuccess)
                return syncResult;
            
            await handler.Handle(syncResult.Actions, cancellationToken);

            iterationCount++;
            if (iterationCount > 10)
            {
                return syncResult;
            }
        }
    }

    private async ValueTask<BucketSyncResult> Push(string id, IEnumerable<BucketSyncFile> files)
    {
        var json = JsonSerializer.Serialize(new { files });
        using var reqContent = new StringContent(json);
        reqContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        var reqMessage = new HttpRequestMessage
        {
            RequestUri = new Uri($"{_host}/buckets/common/{id}/sync"),
            Method = HttpMethod.Post,
            Content = reqContent
        };
        var res = await _httpClient.SendAsync(reqMessage);

        res.EnsureSuccessStatusCode();
        using var resStream = await res.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<BucketSyncResult>(resStream) ?? 
            throw new FormatException();
    }
}