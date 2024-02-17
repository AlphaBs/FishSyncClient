namespace FishSyncClient.Files;

public struct FishPathPair
{
    public FishPathPair(FishPath source, FishPath target) =>
        (Source, Target) = (source, target);

    public FishPath Source;
    public FishPath Target;
}