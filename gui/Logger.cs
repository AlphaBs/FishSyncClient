namespace FishSyncClient.Gui;

public class Logger
{
    public static readonly Logger Instance = new Logger();

    public event EventHandler<string>? Append;

    public void LogInformation(string message)
    {
        log("INFO", message);
    }

    public void LogError(string message)
    {
        log("ERROR", message);
    }

    private void log(string level, string message)
    {
        var log = $"[{level}][{DateTime.Now:HH:mm:ss.ff}] {message}";
        Append?.Invoke(this, log);
    }
}