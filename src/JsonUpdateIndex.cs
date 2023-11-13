using System.Text.Json;

namespace AlphabetUpdater;

public class JsonUpdateIndex : IUpdateIndex
{
    private readonly ITaskFromJsonExtractor _taskExtractor;
    private readonly JsonDocument _json;

    public JsonUpdateIndex(JsonDocument json, ITaskFromJsonExtractor extractor)
    {
        _json = json;
        _taskExtractor = extractor;
 
        Name = json.RootElement.GetProperty("name").GetString();
        LastUpdate = json.RootElement.GetProperty("lastUpdate").GetDateTime();
        ChecksumAlgorithm = json.RootElement.GetProperty("checksumAlgorithm").GetString();
    }

    public string? Name { get; }
    public DateTime LastUpdate { get; }
    public string? ChecksumAlgorithm { get; }

    public IEnumerable<LinkedTask> ExtractTasks()
    {
        var files = _json.RootElement.GetProperty("files").EnumerateArray();
        foreach (var file in files)
        {
            var task = _taskExtractor.ExtractTask(file);
            if (task != null)
                yield return task;
        }
    }
}
