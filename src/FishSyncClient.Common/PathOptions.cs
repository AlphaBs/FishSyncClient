namespace FishSyncClient.Common;

public class PathOptions
{
    public char PathSeperator { get; set; } = '/';
    public char AltPathSeperator { get; set; } = '\\';

    public bool CaseInsensitivePath { get; set; } = true;
}