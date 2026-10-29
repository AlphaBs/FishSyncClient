using DotNet.Globbing;

namespace FishSyncClient.Cli;

public static class SyncWarnings
{
    public static bool Confirm(
        IEnumerable<string> paths,
        IEnumerable<string> patterns,
        bool assumeYes,
        TextReader input,
        TextWriter output)
    {
        var globs = patterns
            .Select(Glob.Parse)
            .ToArray();
        if (globs.Length == 0)
        {
            return true;
        }
        var matches = paths
            .Select(path => path.Replace('\\', '/'))
            .Where(path => globs.Any(glob => glob.IsMatch(path)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (matches.Length == 0)
        {
            return true;
        }

        output.WriteLine("Warning: these files match WarningPatterns and will be synced if you continue:");
        foreach (var path in matches)
        {
            output.WriteLine($"  {path}");
        }
        if (assumeYes)
        {
            output.WriteLine("-y: continuing without confirmation.");
            return true;
        }

        while (true)
        {
            output.Write("Continue? [Y/n] ");
            output.Flush();
            var answer = input.ReadLine();
            if (answer == null)
            {
                output.WriteLine("Input closed; sync cancelled. Pass -y for unattended runs.");
                return false;
            }
            answer = answer.Trim();
            if (answer.Length == 0 || answer.Equals("y", StringComparison.OrdinalIgnoreCase)
                || answer.Equals("yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (answer.Equals("n", StringComparison.OrdinalIgnoreCase)
                || answer.Equals("no", StringComparison.OrdinalIgnoreCase))
            {
                output.WriteLine("Sync cancelled.");
                return false;
            }
            output.WriteLine("Please answer Y or n.");
        }
    }
}
