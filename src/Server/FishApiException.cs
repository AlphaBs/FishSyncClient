namespace FishSyncClient.Server;

public class FishApiException : Exception
{
    public FishApiException(string message, int status) : base(message)
    {
        Status = status;
    }

    public FishApiException(ProblemDetails problem) : base($"{problem.Title ?? "Error"}: {problem.Detail} ({problem.Status})")
    {
        Type = problem.Type;
        Title = problem.Title;
        Detail = problem.Detail;
        Instance = problem.Instance;
        Status = problem.Status;
    }

    public string? Type { get; }
    public string? Title { get; }
    public string? Detail { get; }
    public string? Instance { get; }
    public int Status { get; }
}
