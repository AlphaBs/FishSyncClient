namespace AlphabetUpdater;

public interface ITaskTransformer
{
    IEnumerable<LinkedTask> Transform(IEnumerable<LinkedTask> tasks);
}
