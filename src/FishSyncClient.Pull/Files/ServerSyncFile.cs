using FishSyncClient.Common;

namespace FishSyncClient;

public record ServerSyncFile : SyncFile
{
    public ServerSyncFile(RootedPath path) : base(path)
    {

    }

    public DateTimeOffset Uploaded { get; set; }
    public Uri? Location { get; set; }
}