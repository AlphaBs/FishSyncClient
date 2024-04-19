using System.Text.Json;
using FishSyncClient.Server.Alphabet;

namespace FishSyncClient.Server;

public class AlphabetFileUpdateServer
{
    public static async Task<LauncherMetadata> GetLauncherMetadata(HttpClient httpClient, Uri host)
    {
        var res = await httpClient.GetAsync(host);
        var resStream = await res.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<LauncherMetadata>(resStream) 
            ?? new LauncherMetadata();
    }
}