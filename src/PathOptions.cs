namespace FishSyncClient;

public class PathOptions
{
    public char PathSeparator { get; set; } = '/';
    public char AltPathSeparator { get; set; } = '\\';
    public bool CaseInsensitive { get; set; } = true;
}