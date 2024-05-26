﻿using FishSyncClient.Files;

namespace FishSyncClient.FileComparers;

public class FileSizeComparer : IFileComparer
{
    public ValueTask<bool> AreEqual(SyncFilePair pair, CancellationToken cancellationToken)
    {
        var sourceSize = getSize(pair.Source);
        var targetSize = getSize(pair.Target);
        return new ValueTask<bool>(sourceSize == targetSize);
    }

    private long getSize(SyncFile file)
    {
        if (file.Metadata != null && file.Metadata.Size > 0)
        {
            return file.Metadata.Size;
        }
        else if (file.Path.IsRooted)
        {
            var fileInfo = new FileInfo(file.Path.GetFullPath());
            return fileInfo.Length;
        }
        else
        {
            return 0;
        }
    }
}