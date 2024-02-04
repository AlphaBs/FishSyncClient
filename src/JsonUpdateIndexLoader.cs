using System.Text.Json;

namespace FishSyncClient;

public class JsonUpdateIndexLoader
{
    public async ValueTask<IUpdateIndex> Load(Stream stream)
    {
        var json = await JsonDocument.ParseAsync(stream);
        var index = new JsonUpdateIndex(json, new TaskFromJsonExtractor());
        return index;
    }

    public async ValueTask<IUpdateIndex> LoadFromUrl(Uri uri, HttpClient httpClient)
    {

    }
}
