namespace FishSyncClient.Syncer;

public class FileIntegrityException : Exception
{
    public FileIntegrityException(string file) : base("File integrity error: " + file)
    {
        File = file;
    }

    public FileIntegrityException(string file, string message) : base(message)
    {
        File = file;
    }

    public string File { get; set; }
}
