namespace FishSyncClient;

public record FishPath
{
    public FishPath(RootedPath path)
    {
        Path = path;
    }

    public RootedPath Path { get; }

    public override string ToString()
    {
        return Path.ToString();
    }
}