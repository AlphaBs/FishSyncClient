namespace FishSyncClient;

public class FishPathComparer : IEqualityComparer<FishPath>
{
    public bool Equals(FishPath x, FishPath y)
    {
        return x.Path == y.Path;
    }

    public int GetHashCode(FishPath obj)
    {
        return obj.Path.GetHashCode();
    }
}