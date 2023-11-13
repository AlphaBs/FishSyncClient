using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

public class StringBenchmark
{
    private static RegexOptions options = RegexOptions.None;
    private static readonly char replaceCh = '/';

    [Params("/aaa//aaa/a/aa/a////a/a/aaa/a////a//aa a/sdf/a / /  /// asd/ // /", "as//ev", "a/b/c/d/e/f/g/h/i")]
    public string Input { get; set; } = "";

    private static readonly Regex regex = new Regex($"[{replaceCh}]{{2,}}", options);

    [Benchmark]
    public string RemoveDuplicatedUsingRegex()
    {
        return regex.Replace(Input, replaceCh.ToString());
    }

    [Benchmark]
    public string RemoveDuplicatedUsingStringBuilder()
    {
        var sb = new StringBuilder(Input.Length);
        var findCh = false;
        foreach (var nextCh in Input)
        {
            if (nextCh == replaceCh)
            {
                if (findCh)
                    continue;
                else
                    findCh = true;
            }
            else
            {
                findCh = false;
            }

            sb.Append(nextCh);
        }
        return sb.ToString();
    }
}