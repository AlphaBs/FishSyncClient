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

    public static string NormalizePath(string path, PathOptions options)
    {
        // https://source.dot.net/#System.Private.CoreLib/src/libraries/Common/src/System/IO/PathInternal.cs,d035753e01aa9ae2
        
        var sb = new StringBuilder(path.Length);
        var findCh = false;
        foreach (var _ch in path)
        {
            var ch = _ch;

            // replace AltPathSeperator -> PathSeperator
            if (ch == options.AltPathSeperator)
                ch = options.PathSeperator;

            // skip redundant consecutive path separators
            if (ch == options.PathSeperator)
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

            // case insensitivity
            if (options.CaseInsensitivePath)
                ch = char.ToLowerInvariant(ch);

            sb.Append(ch);
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
}