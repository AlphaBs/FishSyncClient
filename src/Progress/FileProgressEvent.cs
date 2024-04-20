namespace FishSyncClient.Progress;

public enum FileProgressEventType
{
    None,
    Queue,
    StartCompare,
    DoneCompare,
    StartSync,
    DoneSync,
}

public class FileProgressEvent
{
    public FileProgressEvent(
        FileProgressEventType type, 
        int progressed, 
        int total, 
        string current)
    {
        EventType = type;
        ProgressedFiles = progressed;
        TotalFiles = total;
        CurrentFileName = current;
    }

    public FileProgressEventType EventType { get; }
    public int ProgressedFiles { get; }
    public int TotalFiles { get; }
    public string CurrentFileName { get; }
}