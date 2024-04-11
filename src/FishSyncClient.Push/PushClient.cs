using FishSyncClient.Common;

namespace FishSyncClient.Push;

public class PushClient
{
    public BucketSyncFile CreateSyncFile(RootedPath path)
    {
        var fileinfo = new FileInfo(path.GetFullPath());
        using var fs = File.OpenRead(fileinfo.FullName);
        var checksum = ChecksumAlgorithms.ComputeMD5(fs);

        return new BucketSyncFile
        {
            Path = path.SubPath,
            Size = fileinfo.Length,
            Checksum = checksum,
        };
    }

    public async ValueTask<BucketSyncResult> Push(IEnumerable<BucketSyncFile> files, IBucketSyncActionHandler handler)
    {
        int iterationCount = 0;
        while (true)
        {
            var syncResult = await Push(files);
            if (syncResult.IsSuccess)
                return syncResult;
            
            var cannotHandles = syncResult.Actions
                .Where(action => !handler.CanHandle(action));
            if (cannotHandles.Any())
                return syncResult;

            foreach (var action in syncResult.Actions)
            {
                await handler.Handle(action);
            }

            iterationCount++;
            if (iterationCount > 10)
            {
                return syncResult;
            }
        }
    }

    private async ValueTask<BucketSyncResult> Push(IEnumerable<BucketSyncFile> files)
    {

    }
}