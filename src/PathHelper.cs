using System.Text;

namespace FishSyncClient;

// 정규화된 경로
// 불필요하게 중복되는 경로 구분자가 없어야 한다.
// 디렉토리를 나타내는 경로의 끝 문자는 항상 경로 구분자이고, 
// 파일을 나타내는 경로의 끝 문자는 경로 구분자가 될 수 없다.
// 경로 구분자는 한 가지로 통일해야 한다. 

public static class PathHelper
{
    public static string NormalizeDirectoryPath(string path, PathOptions options)
    {
        path = NormalizePath(path, options);
        if (!path.EndsWith(options.PathSeperator))
            path += options.PathSeperator;
        return path;
    }

    public static string NormalizePath(ReadOnlySpan<char> path, PathOptions options)
    {
        // https://source.dot.net/#System.Private.CoreLib/src/libraries/Common/src/System/IO/PathInternal.cs,d035753e01aa9ae2

        var sb = new StringBuilder(path.Length);
        for (int i = -1; i < path.Length; i++)
        {
            char c = (i < 0) ? '/' : path[i];

            if (IsPathSeparator(c, options) && i + 1 < path.Length)
            {
                // skip redundant consecutive path separators
                // e.g. "parent//child" -> "parent/child"
                if (IsPathSeparator(path[i + 1], options))
                    continue;

                // skip . path
                // e.g. "parent/./child" -> "parent/child"
                if ((i + 2 == path.Length || IsPathSeparator(path[i + 2], options)) && path[i + 1] == '.')
                {
                    i++;
                    continue;
                }

                // handle .. path
                // e.g. "parent/child/../grandchild" => "parent/grandchild"
                if (i + 2 < path.Length &&
                    (i + 3 == path.Length || IsPathSeparator(path[i + 3], options)) &&
                    path[i + 1] == '.' &&
                    path[i + 2] == '.')
                {
                    throw new ArgumentException("The path contains relative parent segment.");
                }
            }

            if (i < 0)
                continue;

            // replace AltPathSeperator -> PathSeperator
            if (c == options.AltPathSeperator)
                c = options.PathSeperator;
            // case insensitivity
            else if (options.CaseInsensitivePath)
                c = char.ToLowerInvariant(c);

            sb.Append(c);
        }

        return sb.ToString();
    }

    public static bool IsPathSeparator(char ch, PathOptions pathOptions)
    {
        return ch == pathOptions.PathSeperator || ch == pathOptions.AltPathSeperator;
    }

    public static string GetRelativePathFromDirectory(string absPath, string rootPath, PathOptions options)
    {
        absPath = NormalizePath(absPath, options);
        if (string.IsNullOrEmpty(rootPath))
            return absPath;

        if (!rootPath.EndsWith(options.PathSeperator))
            throw new ArgumentException("rootPath is not a directory");
        rootPath = NormalizePath(rootPath, options);

        if (!IsRootDirectory(rootPath, absPath, options))
            throw new ArgumentException("absPath should start with rootPath");

        if (absPath.Length == rootPath.Length)
            return string.Empty;
        else
            return absPath.Substring(rootPath.Length);
    }

    public static bool IsRootDirectory(string rootDir, string filePath, PathOptions options)
    {
        var comparisonType = options.CaseInsensitivePath
             ? StringComparison.OrdinalIgnoreCase
             : StringComparison.Ordinal;

        int rootDirLength = rootDir.Length;
        return filePath.StartsWith(rootDir, comparisonType) &&
            (rootDir[rootDirLength - 1] == options.PathSeperator ||
            filePath.IndexOf(options.PathSeperator, rootDirLength) == rootDirLength);
    }

    public static void CreateParentDirectory(string path)
    {
        var dirPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dirPath))
            Directory.CreateDirectory(dirPath);
    }
}