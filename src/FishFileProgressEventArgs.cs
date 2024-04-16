namespace FishSyncClient;

public enum FishFileProgressEventType
{
    None,
    Start,
    Done
}

public class FishFileProgressEventArgs
{
    public FishFileProgressEventArgs(FishFileProgressEventType type, int progressed, int total, string current)
    {
        EventType = type;
        ProgressedFiles = progressed;
        TotalFiles = total;
        CurrentFileName = current;
    }

    public FishFileProgressEventType EventType { get; }
    public int ProgressedFiles { get; }
    public int TotalFiles { get; }
    public string CurrentFileName { get; }
}