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

        _pathOptions = pathOptions;
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

            try
            {
                var pattern = rule.Pattern?
                    .Replace(_pathOptions.AltPathSeparator, _pathOptions.PathSeparator)
                    .Replace(_pathOptions.PathSeparator, '/');
                var glob = Glob.Parse(pattern!, globOptions);
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

    private static ArgumentException InvalidRule(int index, string field, string message, Exception? inner = null) =>
        new($"Rules[{index}].{field}: {message}.", "Rules", inner);
}
