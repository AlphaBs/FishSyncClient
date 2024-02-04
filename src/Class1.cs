namespace FishSyncClient;

public class Class1
{
    async void a()
    {
        var pathOptions = new PathOptions();

        var loader = new JsonUpdateIndexLoader();
        var index = await loader.Load(File.OpenRead("update-file-index.json"));
        var updateTasks = index.ExtractTasks();

        var localFiles = RootedPathIndex.CreateFromDirectory("./test-dir", pathOptions);
        foreach (var task in updateTasks)
        {
            localFiles.Remove(task.Path);
        }

        var deleteTasks = localFiles.Paths.Select(f => createDeleteTask(f));
        handleTasks(updateTasks);
        handleTasks(deleteTasks);
    }

    LinkedTask createDeleteTask(RootedPath path)
    {

    }

    void handleTasks(IEnumerable<LinkedTask> tasks)
    {

    }
}
