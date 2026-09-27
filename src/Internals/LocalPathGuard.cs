namespace FishSyncClient.Internals;

/// <summary>Reject links before accessing local paths, including links in ancestor directories.</summary>
internal static class LocalPathGuard
{
    public static void EnsureNoLinks(string path)
    {
        if (!System.IO.Path.IsPathFullyQualified(path))
            throw new ArgumentException("Local paths must be absolute.", nameof(path));

        var fullPath = System.IO.Path.GetFullPath(path);
        var root = System.IO.Path.GetPathRoot(fullPath)!;
        var current = root;
        CheckEntry(current);
        foreach (var segment in fullPath.Substring(root.Length).Split(
                     new[] { System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = System.IO.Path.Combine(current, segment);
            CheckEntry(current);
        }
    }

    private static void CheckEntry(string path)
    {
        FileAttributes attributes;
        try
        {
            attributes = File.GetAttributes(path);
        }
        catch (FileNotFoundException) { return; }
        catch (DirectoryNotFoundException) { return; }

        if ((attributes & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Symbolic links and reparse points are not allowed: {path}");
    }
}
