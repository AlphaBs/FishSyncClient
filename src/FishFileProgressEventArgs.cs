namespace FishSyncClient;

public class FishFileProgressEventArgs
{
    public FishFileProgressEventArgs(int progressed, int total, RootedPath current)
    {
        ProgressedFiles = progressed;
        TotalFiles = total;
        CurrentFile = current;
    }

    public int ProgressedFiles { get; }
    public int TotalFiles { get; }
    public RootedPath CurrentFile { get; }
}