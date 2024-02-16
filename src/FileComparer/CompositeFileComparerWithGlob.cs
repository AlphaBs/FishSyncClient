
using DotNet.Globbing;

namespace FishSyncClient.FileComparers;

public class CompositeFileComparerWithGlob : IFileComparer
{
    struct GlobComparerPair
    {
        public Glob Glob;
        public IFileComparer FileComparer;
    }

    private readonly List<GlobComparerPair> _globComparerPairs = new();

    public void Add(string pattern, IFileComparer comparer)
    {
        var glob = Glob.Parse(pattern);
        _globComparerPairs.Add(new GlobComparerPair
        {
            Glob = glob,
            FileComparer = comparer
        });
    }

    public async ValueTask<bool> CompareFile(string path, FishFileMetadata file)
    {
        foreach (var globComparerPair in _globComparerPairs)
        {
            var isMatch = globComparerPair.Glob.IsMatch(file.Path.SubPath);
            if (isMatch)
            {
                return await globComparerPair.FileComparer.CompareFile(path, file);
            }
        }
        return true;
    }
}