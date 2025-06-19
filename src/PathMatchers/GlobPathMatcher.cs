using DotNet.Globbing;

namespace FishSyncClient.PathMatchers;

public class GlobPathMatcher : IPathMatcher
{
    private readonly Glob _pattern;

    public GlobPathMatcher(string pattern)
    {
        _pattern = Glob.Parse(pattern);
    }

    public bool Match(string subPath)
    {
        return _pattern.IsMatch(subPath);
    }
}
