namespace FishSyncClient.Server;

public class LocalPushClient
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
}