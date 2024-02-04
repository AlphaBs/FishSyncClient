using System.Text.Json;

namespace FishSyncClient;

public interface ITaskFromJsonExtractor
{
    LinkedTask? ExtractTask(JsonElement json);
}
