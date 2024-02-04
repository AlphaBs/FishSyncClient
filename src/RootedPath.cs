namespace FishSyncClient;

// RootedPath 는 경로를 Root 와 SubPath 두 부분으로 나눈다.
// 디렉토리를 나타내는 경로의 끝 문자는 항상 경로 구분자이고, 
// 파일을 나타내는 경로의 끝 문자는 경로 구분자가 될 수 없다.
//   1) Root 가 대상의 상위 경로 중 임의의 경로인 경우,
//      SubPath 는 Root 를 기준으로 대상의 상대 경로를 나타낸다.
//   2) Root 가 정해지지 않은 경로인 경우,
//      Root 는 공백 문자열을 가진다.
//   3) 대상의 경로와 Root 같은 경로를 가질 때, 
//      Root 는 대상의 경로를 그대로 가지고 SubPath 는 빈 문자열을 가진다.
// SubPath 의 첫 문자는 경로 구분자가 될 수 없다.
// Root 와 SubPath 는 엄격하게 정규화된 경로를 유지해야 한다.

public struct RootedPath
{
    public static RootedPath Create(string root, string subpath)
    {

    }

    public static RootedPath FromFullPath(string root, string fullpath)
    {

    }

    public static RootedPath FromSubPath(string subpath)
    {
        return new RootedPath(string.Empty, subpath);
    }

    private RootedPath(string root, string subpath)
    {
        Root = root;
        SubPath = subpath;
    }

    public string Root { get; }
    public string SubPath { get; }

    public bool IsRooted => Root.Equals(string.Empty);
    public bool IsDirectory => SubPath.EndsWith('/') || SubPath.Equals(string.Empty);

    public RootedPath WithRoot(string newRoot)
    {
        return new RootedPath(newRoot, SubPath);
    }

    public string GetFullPath()
    {
        return Path.Combine(Root, SubPath);
    }
}
