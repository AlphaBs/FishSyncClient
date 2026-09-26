namespace FishSyncClient;

public sealed record PathOptions
{
    public char PathSeparator { get; init; } = '/';
    public char AltPathSeparator { get; init; } = '\\';
    public bool CaseInsensitive { get; init; } = true;
}
