namespace FishSyncClient.PathMatchers;

public class RulePathMatcher : IPathMatcher
{
    private readonly struct MatchRule
    {
        public MatchRule(bool include, IPathMatcher matcher) => 
            (Include, Matcher) = (include, matcher);

        public readonly bool Include;
        public readonly IPathMatcher Matcher;
    }

    private readonly List<MatchRule> _rules = new();

    public RulePathMatcher AddIncludeRule(IPathMatcher matcher)
    {
        _rules.Add(new MatchRule(true, matcher));
        return this;
    }

    public RulePathMatcher AddExcludeRule(IPathMatcher matcher)
    {
        _rules.Add(new MatchRule(false, matcher));
        return this;
    }

    public bool Match(string subPath)
    {
        foreach (var rule in _rules)
        {
            if (rule.Matcher.Match(subPath))
            {
                return rule.Include;
            }
        }

        return true;
    }
}
