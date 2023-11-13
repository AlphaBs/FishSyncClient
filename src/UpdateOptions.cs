namespace AlphabetUpdater;

public class UpdateOptions
{
    public bool ForceUpdate { get; set; } = false;
    public IEnumerable<RootedPath> TempUserFiles { get; set; } = Enumerable.Empty<RootedPath>();
    public IEnumerable<RootedPath> PersistentUserFiles { get; set; } = Enumerable.Empty<RootedPath>();
    public IEnumerable<RootedPath> ForceUpdateFiles { get; set; } = Enumerable.Empty<RootedPath>();
}