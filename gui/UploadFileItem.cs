using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using FishSyncClient.Progress;

namespace FishSyncClient.Gui;

public sealed record FileUploadProgress(string Path, string? State = null, ByteProgress Bytes = default);

public partial class UploadFileItem(FileChange change) : ObservableObject
{
    public string Path => change.Path;
    public string Kind => change.Kind;
    public string SizeText => change.SizeText;
    public IBrush Background => change.Background;
    [ObservableProperty] private double percent;
    [ObservableProperty] private string progressText = change.Kind == "삭제" ? "서버 반영 대기" : "대기";
    private long _sent;
    private bool _transferred;
    private bool _finished;

    public void Apply(FileUploadProgress update)
    {
        if (_finished) return;
        if (update.State == "대기") { _sent = 0; _transferred = false; Percent = 0; }
        if (update.State == "전송 완료") { _transferred = true; Percent = 100; }
        if (update.State != null) ProgressText = update.State is "업로드 중" or "전송 완료" ? $"{Percent:0}%" : update.State;
        if (update.Bytes.ProgressedBytes > 0 && !_transferred)
        {
            _sent += update.Bytes.ProgressedBytes;
            Percent = change.Size > 0 ? Math.Clamp(100d * _sent / change.Size, 0, 100) : 0;
            ProgressText = $"{Percent:0}%";
        }
    }

    public void Finish(bool success, bool cancelled = false)
    {
        _finished = true;
        if (success) { Percent = 100; ProgressText = "반영 완료"; }
        else ProgressText = _transferred ? "전송 완료 · 반영 미확인" : cancelled ? "취소됨" : "중단됨";
    }
}
