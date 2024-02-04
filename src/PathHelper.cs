using System.Text;

namespace FishSyncClient;

// 정규화된 경로
// 불필요하게 중복되는 경로 구분자가 없어야 한다
// 디렉토리를 나타내는 경로의 끝 문자는 항상 경로 구분자이고, 
// 파일을 나타내는 경로의 끝 문자는 경로 구분자가 될 수 없다.

public static class PathHelper
{
    public static string NormalizeDirectoryPath(string path, PathOptions options)
    {
        path = NormalizePath(path, options);
        if (!path.EndsWith(options.PathSeperator))
            path += options.PathSeperator;
        return path;
    }

    public static string NormalizePath(string path, PathOptions options)
    {
        path = removeDuplicatedCharacter(path, options.PathSeperator);
        if (options.CaseInsensitivePath)
            path = path.ToLowerInvariant();
        return path; 
    }

    private static string removeDuplicatedCharacter(ReadOnlySpan<char> input, char replaceCh)
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

    public static string GetRelativePathFromDirectory(string absPath, string rootPath, PathOptions options)
    {
        absPath = NormalizePath(absPath, options);
        if (string.IsNullOrEmpty(rootPath))
            return absPath;

        if (!rootPath.EndsWith(options.PathSeperator))
            throw new ArgumentException("rootPath is not a directory");
        rootPath = NormalizePath(rootPath, options);

        if (!absPath.StartsWith(rootPath))
            throw new ArgumentException("absPath should start with rootPath");

        if (absPath.Length == rootPath.Length)
            return string.Empty;
        else
            return absPath.Substring(rootPath.Length);
    }
}