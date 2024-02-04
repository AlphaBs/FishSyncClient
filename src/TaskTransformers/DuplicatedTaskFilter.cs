namespace FishSyncClient;

public class DuplicatedTaskFilter : ITaskTransformer
{
    public IEnumerable<LinkedTask> Transform(IEnumerable<LinkedTask> tasks)
    {
        var set = new HashSet<string>();
        return tasks.Where(t => set.Add(t.Path.SubPath));
    }
}
