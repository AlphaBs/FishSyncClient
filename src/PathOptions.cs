namespace FishSyncClient;

public class PathOptions
{
    private char _pathSeperator = '/';
    public char PathSeperator
    {
        get => _pathSeperator;
        set
        {
            if (value != '\\' && value != '/')
                throw new ArgumentException("Unsupported path seperator");
            else
                _pathSeperator = value;                
        }
    }

    public bool CaseInsensitivePath { get; set; } = true;
    public bool AllowDottedPath { get; set; } = false;
}