using System.Text;

namespace AlphabetUpdater;

public class PathOptions
{
    public PathOptions()
    {
        PathSeperator = Path.PathSeparator;
    }

    private char _pathSeperator;
    public char PathSeperator
    {
        get => _pathSeperator;
        set
        {
            if (value != '\\' && value != '/')
                _pathSeperator = value;
            else
                throw new ArgumentException("Unsupported path seperator");
        }
    }

    public bool CaseInsensitivePath { get; set; } = true;
}

public static class PathHelper
{
    public static string NormalizeDirectoryPath(string path, PathOptions options)
    {
        return NormalizePath(path, options) + options.PathSeperator;
    }

    public static string NormalizePath(string path, PathOptions options)
    {
        path = path.TrimStart().Trim(options.PathSeperator);
        path = removeDuplicatedCharacter(path, options.PathSeperator);
        if (options.CaseInsensitivePath)
            path = path.ToLowerInvariant();
        return path;
    }

    private static string removeDuplicatedCharacter(string input, char replaceCh)
    {
        var sb = new StringBuilder(input.Length);
        var findCh = false;
        foreach (var nextCh in input)
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

    public static string GetRelativePathFromAbsolutePath(string absPath, string rootPath, PathOptions options)
    {
        absPath = NormalizePath(absPath, options);
        if (!absPath.StartsWith(rootPath))
            throw new ArgumentException("The absPath should start with rootPath");
        return absPath.Substring(rootPath.Length + 1);
    }

    public static HashSet<string> ToNormalizedSet(IEnumerable<string> path, PathOptions options) 
        => new HashSet<string>(path.Select(p => NormalizePath(p, options)));
}