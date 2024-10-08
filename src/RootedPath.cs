﻿using System.Diagnostics;

namespace FishSyncClient;

// RootedPath 는 경로를 Root 와 SubPath 두 부분으로 나눈다.
//   1) Root 가 대상의 상위 경로 중 일부인 경우,
//      SubPath 는 Root 를 기준으로 대상의 상대 경로를 나타낸다.
//      이때 Root 는 항상 디렉토리를 나타내며 경로 구분자로 끝난다.
//   2) Root 가 정해지지 않았을 경우,
//      Root 는 공백 문자열을 가진다.
//      이때 SubPath 는 공백 문자열을 가질 수 없다.
//   3) 대상이 디렉토리를 나타내며 동시에 대상이 Root 와 같은 경로를 가질 때, 
//      Root 는 대상의 경로를 그대로 가지고 SubPath 는 빈 문자열을 가진다.
// SubPath 의 첫 문자는 경로 구분자가 될 수 없다.
// SubPath 에는 상대 경로 문자 (. 혹은 ..) 가 들어갈 수 없다. 
// Root 와 SubPath 는 정규화된 경로를 유지해야 한다.
// Root 는 절대경로이다. Path.IsPathFullyQualified -> true

public readonly struct RootedPath
{
    public static RootedPath Create(string root, string subpath, PathOptions options)
    {
        if (string.IsNullOrEmpty(root))
        {
            if (string.IsNullOrEmpty(subpath))
                throw new ArgumentException("The subpath cannot be an empty string with empty root");
        }
        else
        {
            root = PathHelper.NormalizeRoot(root, options);
        }

        subpath = PathHelper.NormalizePath(subpath, options);
        subpath = subpath.TrimStart(options.PathSeparator);

        return new RootedPath(root, subpath, options);
    }

    public static RootedPath FromFullPath(string root, string fullpath, PathOptions options)
    {
        root = PathHelper.NormalizeRoot(root, options);
        var subpath = PathHelper.GetRelativePathFromDirectory(fullpath, root, options);
        return Create(root, subpath, options);
    }

    public static RootedPath FromSubPath(string subpath, PathOptions options)
    {
        return Create(string.Empty, subpath, options);
    }

    public static IEnumerable<RootedPath> FromDirectory(string root, PathOptions options)
    {
        if (!Directory.Exists(root))
            yield break;

        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
        foreach (var item in files)
        {
            yield return FromFullPath(root, item, options);
        }
    }

    private readonly PathOptions _options;

    private RootedPath(string root, string subpath, PathOptions options)
    {
        Debug.Assert(!subpath.StartsWith(options.PathSeparator));

        Root = root;
        SubPath = subpath;
        _options = options;
    }

    public readonly string Root;
    public readonly string SubPath;

    public bool IsRooted => !string.IsNullOrEmpty(Root);
    public bool IsDirectory => SubPath.EndsWith(_options.PathSeparator) || SubPath.Equals(string.Empty);

    public RootedPath WithRoot(string newRoot)
    {
        return Create(newRoot, SubPath, _options);
    }

    public string GetFullPath()
    {
        if (!IsRooted)
            throw new InvalidOperationException("Root was not set");
        return Root + SubPath;
    }

    public override string ToString()
    {
        if (IsRooted)
            return GetFullPath();
        else
            return SubPath;
    }

    public override int GetHashCode()
    {
        return ToString().GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return obj.ToString() == this.ToString();
    }

    public static bool operator ==(RootedPath a, RootedPath b)
    {
        return a.ToString() == b.ToString();
    }

    public static bool operator !=(RootedPath a, RootedPath b)
    {
        return a.ToString() != b.ToString();
    }
}
