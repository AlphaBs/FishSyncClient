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

        output.WriteLine("경고: WarningPatterns에 일치하는 파일이 있습니다. 계속하면 동기화에 포함됩니다.");
        foreach (var path in matches)
        {
            output.WriteLine($"  {path}");
        }
        if (assumeYes)
        {
            output.WriteLine("-y: 확인 입력을 생략하고 계속합니다.");
            return true;
        }

        while (true)
        {
            output.Write("계속하시겠습니까? [Y/n] ");
            output.Flush();
            var answer = input.ReadLine();
            if (answer == null)
            {
                output.WriteLine("입력이 종료되어 동기화를 취소했습니다. 자동화에서는 -y를 지정해 주세요.");
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
                output.WriteLine("동기화를 취소했습니다.");
                return false;
            }
            output.WriteLine("Y 또는 n을 입력해 주세요.");
        }
    }
}
