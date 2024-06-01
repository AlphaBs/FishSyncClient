namespace FishSyncClient.FileComparers;

public class FileComparerException : Exception
{
    public FileComparerException() : base()
    {
        
    }

    public FileComparerException(string message) : base(message)
    {
        
    }
}