namespace AlphabetUpdater;

public interface IUpdateIndex
{
    string? Name { get; }
    DateTime LastUpdate { get; }
    string? ChecksumAlgorithm { get; }

    IEnumerable<LinkedTask> ExtractTasks();
}