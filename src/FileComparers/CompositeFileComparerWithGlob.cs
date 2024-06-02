using DotNet.Globbing;
using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class CompositeFileComparerWithGlob : IFileComparer
{
    readonly struct GlobComparerPair
    {
        public GlobComparerPair(Glob glob, IFileComparer fileComparer) => 
            (Glob, FileComparer) = (glob, fileComparer);

        public readonly Glob Glob;
        public readonly IFileComparer FileComparer;
    }

    private readonly List<GlobComparerPair> _globComparerPairs = new();
    private readonly ComparerErrorHandlingModes _errorMode = ComparerErrorHandlingModes.ThrowException;

    public CompositeFileComparerWithGlob() : this(ComparerErrorHandlingModes.ThrowException)
    {
        
    }

    public CompositeFileComparerWithGlob(ComparerErrorHandlingModes mode)
    {
        _errorMode = mode;
    }

    public void Add(string pattern, IFileComparer comparer)
    {
        var glob = Glob.Parse(pattern);
        _globComparerPairs.Add(new GlobComparerPair(glob, comparer));
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

        switch (_errorMode)
        {
            case ComparerErrorHandlingModes.ReturnEqual:
                return true;
            case ComparerErrorHandlingModes.ReturnNotEqual:
                return false;
            case ComparerErrorHandlingModes.ThrowException:
            default:
                throw new FileComparerException("No matching pattern: " + pair.Source.Path.SubPath);
        }
    }
}