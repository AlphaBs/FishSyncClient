using System.Text.Json;

namespace AlphabetUpdater;

public interface ITaskFromJsonExtractor
{
    LinkedTask? ExtractTask(JsonElement json);
}
