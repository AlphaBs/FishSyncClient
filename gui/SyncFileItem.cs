using CommunityToolkit.Mvvm.ComponentModel;
using FishSyncClient.Files;
using FishSyncClient.Progress;

namespace FishSyncClient.Gui;

internal partial class SyncFileItem : ObservableObject
{
    public SyncFileItem(SyncFile file)
    {
        File = file;
    }

    public SyncFile File { get; }
    public string Name => File.Path.SubPath;
    public long Size => File.Metadata?.Size ?? 0;
    public string? Checksum => File.Metadata?.Checksum;
    public bool IsProgressing { get; set; }
    public ByteProgress CurrentProgress { get; set; }

    [ObservableProperty]
    private string status = "대기";
}
