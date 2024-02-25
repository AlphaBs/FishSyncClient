using DotNet.Globbing;
using FishSyncClient.Files;

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

    public async ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        foreach (var globComparerPair in _globComparerPairs)
        {
            var isMatch = globComparerPair.Glob.IsMatch(pair.Source.Path.SubPath);
            if (isMatch)
            {
                return await globComparerPair.FileComparer.AreEqual(pair, cancellationToken);
            }
        }
        return true;
    }
}