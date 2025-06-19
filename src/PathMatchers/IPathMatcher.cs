namespace FishSyncClient.PathMatchers;

public interface IPathMatcher
{
    bool Match(string subPath);
}
