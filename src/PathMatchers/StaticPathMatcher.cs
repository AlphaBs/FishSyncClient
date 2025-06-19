namespace FishSyncClient.PathMatchers;

public class StaticPathMatcher : IPathMatcher
{
    private static Lazy<IPathMatcher> _matchAll = new(() => new StaticPathMatcher(true));
    private static Lazy<IPathMatcher> _matchNothing = new(() => new StaticPathMatcher(false));
    public static IPathMatcher MatchAll() => _matchAll.Value;
    public static IPathMatcher MatchNothing() => _matchNothing.Value;

    private readonly bool _returnValue;
    private StaticPathMatcher(bool returnValue) => _returnValue = returnValue;
    public bool Match(string subPath) => _returnValue;
}
