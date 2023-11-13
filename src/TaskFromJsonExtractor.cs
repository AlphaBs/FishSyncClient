using System.Text.Json;

namespace AlphabetUpdater;

public class TaskFromJsonExtractor : ITaskFromJsonExtractor
{
    private readonly HashSet<string> _tempUserFiles;
    private readonly HashSet<string> _persistentUserFiles;
    private readonly HashSet<string> _forceUpdateFiles;

    public TaskFromJsonExtractor(UpdateOptions options)
    {
        if (options.ForceUpdate)
        {
            _tempUserFiles = new HashSet<string>();
            _forceUpdateFiles = new HashSet<string>(new string[] { "/" });
        }
        else
        {
            _tempUserFiles = convertRootedPathsToStringSet(options.TempUserFiles);
            _forceUpdateFiles = convertRootedPathsToStringSet(options.ForceUpdateFiles);
        }
        _persistentUserFiles = convertRootedPathsToStringSet(options.PersistentUserFiles);
    }

    private HashSet<string> convertRootedPathsToStringSet(IEnumerable<RootedPath> paths)
    {
        return new HashSet<string>(paths.Select(p => p.SubPath));
    }

    public LinkedTask? ExtractTask(JsonElement json)
    {
        var file = json.Deserialize<UpdateFileMetadata>();
        if (file == null)
            return null;

        var path = RootedPath.FromSubPath(file.Path);
        if (checkForceUpdateFile(path))
        {
            if (!checkPersistentUserFiles(path))
            {
                return createForceUpdateTask(file);
            }
        }
        else
        {
            if (!checkTempUserFile(path))
            {
                return createUpdateTask(file);
            }
        }
        return null;
    }

    private bool checkTempUserFile(RootedPath path)
        => checkPathInCollection(path, _tempUserFiles);

    private bool checkPersistentUserFiles(RootedPath path)
        => checkPathInCollection(path, _persistentUserFiles);

    private bool checkForceUpdateFile(RootedPath path)
        => checkPathInCollection(path, _forceUpdateFiles);

    private bool checkPathInCollection(RootedPath path, IEnumerable<string> collection)
        => collection.Any(item => path.SubPath.StartsWith(item));

    private LinkedTask createUpdateTask(UpdateFileMetadata file)
    {

    }

    private LinkedTask createForceUpdateTask(UpdateFileMetadata file)
    {

    }
}