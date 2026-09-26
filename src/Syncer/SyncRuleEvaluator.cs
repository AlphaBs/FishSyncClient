using DotNet.Globbing;

namespace FishSyncClient.Syncer;

internal sealed class SyncRuleEvaluator
{
    private readonly List<(Glob Pattern, SyncAction Action, bool Enabled)> _rules = new();
    private readonly PathOptions _pathOptions;

    public SyncRuleEvaluator(SyncerOptions options, PathOptions pathOptions)
    {
        if (options.Rules == null)
            throw new ArgumentException("Rules must be specified. Use an empty list to exclude all files.", nameof(options));
        if (options.Context == null)
            throw new ArgumentException("Context cannot be null.", nameof(options));

        _pathOptions = new PathOptions
        {
            PathSeparator = pathOptions.PathSeparator,
            AltPathSeparator = pathOptions.AltPathSeparator,
            CaseInsensitive = pathOptions.CaseInsensitive
        };
        var globOptions = new GlobOptions
        {
            Evaluation = new EvaluationOptions { CaseInsensitive = _pathOptions.CaseInsensitive }
        };
        var isNewVersion = options.Context.IsNewVersion || options.Context.IsForced;

        // Compile and validate every rule before enumerating files or starting any transfers.
        var rules = options.Rules.ToArray();
        for (var index = 0; index < rules.Length; index++)
        {
            var rule = rules[index];
            if (rule == null)
                throw InvalidRule(index, "rule", "cannot be null");
            if (!Enum.IsDefined(typeof(SyncAction), rule.Action))
                throw InvalidRule(index, "action", "must be fullSync, installOnly, or exclude");
            if (!Enum.IsDefined(typeof(SyncCondition), rule.Condition))
                throw InvalidRule(index, "condition", "must be always or onNewVersion");
            if (string.IsNullOrWhiteSpace(rule.Pattern))
                throw InvalidRule(index, "pattern", "cannot be empty");

            try
            {
                var pattern = Normalize(rule.Pattern);
                ValidatePattern(pattern);
                var glob = Glob.Parse(pattern, globOptions);
                _rules.Add((glob, rule.Action, rule.Condition == SyncCondition.Always || isNewVersion));
            }
            catch (Exception exception) when (exception is ArgumentException || exception is IndexOutOfRangeException)
            {
                throw InvalidRule(index, "pattern", exception.Message, exception);
            }
        }
    }

    public SyncAction Evaluate(string subPath)
    {
        var path = Normalize(subPath);
        foreach (var rule in _rules)
        {
            if (rule.Enabled && rule.Pattern.IsMatch(path))
                return rule.Action;
        }
        return SyncAction.Exclude;
    }

    private string Normalize(string path) =>
        PathHelper.NormalizePath(path, _pathOptions).Replace(_pathOptions.PathSeparator, '/');

    private static void ValidatePattern(string pattern)
    {
        if (pattern.StartsWith('/') || (pattern.Length > 1 && pattern[1] == ':'))
            throw new ArgumentException("must be relative to the sync root");

        // DotNet.Glob accepts unterminated character lists, so reject those explicitly.
        for (var index = 0; index < pattern.Length; index++)
        {
            if (pattern[index] != '[')
                continue;

            var start = index + 1;
            if (start < pattern.Length && pattern[start] == '!')
                start++;
            // A closing bracket as the first list character is a literal bracket.
            if (start < pattern.Length && pattern[start] == ']')
                start++;
            var end = pattern.IndexOf(']', start);
            if (end < 0)
                throw new ArgumentException("contains an unterminated character list");
            index = end;
        }
    }

    private static ArgumentException InvalidRule(int index, string field, string message, Exception? inner = null) =>
        new($"Rules[{index}].{field}: {message}.", "Rules", inner);
}
