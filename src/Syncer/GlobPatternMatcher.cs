using DotNet.Globbing;

namespace FishSyncClient.Syncer;

public class GlobPatternMatcher
{
    public static GlobPatternMatcher ParseFrom(IEnumerable<string> includes, IEnumerable<string> excludes)
    {
        return new GlobPatternMatcher(
            parsePatternsToGlobs(includes), 
            parsePatternsToGlobs(excludes));
    }

    private static IReadOnlyCollection<Glob> parsePatternsToGlobs(IEnumerable<string> patterns)
    {
        return patterns.Select(pattern => Glob.Parse(pattern)).ToList();
    }

    private readonly IReadOnlyCollection<Glob> _includes;
    private readonly IReadOnlyCollection<Glob> _excludes;

    private GlobPatternMatcher(IReadOnlyCollection<Glob> includes, IReadOnlyCollection<Glob> excludes)
    {
        _includes = includes;
        _excludes = excludes;
    }

    public bool Match(string path)
    {
        return _includes.Any(pattern => pattern.IsMatch(path)) &&
               !_excludes.Any(pattern => pattern.IsMatch(path));
    }
}