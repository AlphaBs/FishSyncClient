using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using FishSyncClient.Progress;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;

namespace FishSyncClient.Gui;

public partial class MainWindow : Window
{
    private readonly string _applicationDirectory;
    private readonly HttpClient _http;
    private readonly ConfigManager _config;
    private readonly Action<string>? _folderOpener;
    private readonly Func<string, string, Task<bool>> _confirm;
    private readonly bool _checkUpdates;
    private readonly ObservableCollection<WorkspaceFileItem> _files = [];
    private readonly CancellationTokenSource _lifetime = new();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly DispatcherTimer _listTimer = new() { Interval = TimeSpan.FromSeconds(5) };
    private readonly DispatcherTimer _usageTimer = new() { Interval = TimeSpan.FromSeconds(60) };
    private readonly DispatcherTimer _eventTimer = new() { Interval = TimeSpan.FromMilliseconds(350) };
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly object _progressLock = new();
    private ByteProgress _bytes;
    private string? _pendingStatus;
    private CancellationTokenSource? _operation;
    private FileSystemWatcher? _watcher;
    private BucketSession? _session;
    private Comparison? _preview;
    private bool _busy;
    private bool _refreshingUsage;
    private bool _refreshingList;
    private bool _ready;
    private bool _closed;
    private bool _loaded;
    private bool _watcherFailed;
    private string? _localStamp;
    private string? _remoteStamp;
    private BucketUsage? _usage;
    private long _localBytes;
    private int _localFileCount;
    private Dictionary<string, UploadFileItem> _uploadItems = new(WorkspaceFiles.Comparer);
    private ConcurrentQueue<FileUploadProgress> _fileProgress = new();

    public MainWindow() : this(AppContext.BaseDirectory, HttpUtil.HttpClient, ConfigManager.Instance, checkUpdates: true) { }

    public MainWindow(string applicationDirectory, HttpClient http, ConfigManager config, Action<string>? folderOpener = null,
        Func<string, string, Task<bool>>? confirm = null, bool checkUpdates = false)
    {
        _applicationDirectory = applicationDirectory;
        _http = http;
        _config = config;
        _folderOpener = folderOpener;
        _confirm = confirm ?? (async (message, title) =>
            await MessageBox.Show(message, title, MessageBoxButtons.YesNo) == MessageBoxResult.Yes);
        _checkUpdates = checkUpdates;
        InitializeComponent();
        fileList.ItemsSource = _files;
        _listTimer.Tick += async (_, _) => await RefreshList();
        _usageTimer.Tick += async (_, _) => await RefreshUsage();
        _eventTimer.Tick += async (_, _) => { _eventTimer.Stop(); await RefreshList(); };
        _progressTimer.Tick += (_, _) => FlushProgress();
    }

    private async void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        btnNext.IsEnabled = false;
        var config = await _config.LoadConfig();
        if (_closed) return;
        txtUsername.Text = config.Username;
        txtBucketId.Text = config.BucketId;
        txtHost.Text = string.IsNullOrWhiteSpace(config.Host) ? "https://fish2.snowfrost.kr/api" : config.Host;
        btnNext.IsEnabled = true;
        if (_checkUpdates) await CheckForUpdates();
    }

    private async Task CheckForUpdates()
    {
        try
        {
            if (UpdateService.Platform is not { } os || UpdateService.ArchitectureName is not { } arch) return;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var update = await new UpdateService(_http).Check(os, arch, UpdateService.CurrentVersion, timeout.Token);
            if (update == null || _closed || _busy) return;
            if (await _confirm($"새 버전 {update.Version}이 있습니다. 다운로드 받을까요?", "업데이트") && !_closed)
                Process.Start(new ProcessStartInfo(update.Download.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Instance.LogInformation($"업데이트 확인 또는 다운로드 열기 실패: {ex.Message}");
        }
    }

    private async Task Run(string message, Func<CancellationToken, Task> action)
    {
        if (_busy || _closed) return;
        _busy = true;
        _fileProgress = new();
        _uploadItems.Clear();
        var succeeded = false;
        _operation = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        var ct = _operation.Token;
        lock (_progressLock) { _bytes = default; _pendingStatus = null; }
        statusText.Text = message;
        postUploadNotice.IsVisible = false;
        transferText.Text = "";
        transferBar.IsVisible = true;
        transferBar.Value = 0;
        transferBar.IsIndeterminate = true;
        SetEnabled();
        _progressTimer.Start();
        var acquired = false;
        try
        {
            await _gate.WaitAsync(ct);
            acquired = true;
            await action(ct);
            succeeded = true;
            lock (_progressLock) _pendingStatus = null;
            transferBar.IsIndeterminate = false;
            transferBar.Value = 100;
        }
        catch (OperationCanceledException)
        {
            statusText.Text = "작업을 취소했습니다. 일부 변경이 반영되었을 수 있습니다. 다시 비교하거나 초기화를 재시도해 주세요.";
            MarkStale();
        }
        catch (Exception ex)
        {
            statusText.Text = $"작업을 완료하지 못했습니다: {ex.Message}";
            MarkStale();
        }
        finally
        {
            if (acquired) _gate.Release();
            _progressTimer.Stop();
            FlushProgress(false);
            foreach (var item in _uploadItems.Values) item.Finish(succeeded, ct.IsCancellationRequested);
            transferBar.IsIndeterminate = false;
            _operation.Dispose();
            _operation = null;
            _busy = false;
            if (_ready)
            {
                _preview = null;
                previewPanel.IsVisible = false;
                workspacePanel.IsVisible = true;
            }
            SetEnabled();
            if (_ready && !_closed) await RefreshList();
        }
    }

    private IProgress<string> StatusProgress() => new SynchronousProgress<string>(message =>
    {
        lock (_progressLock) _pendingStatus = message;
    });

    private IProgress<ByteProgress> ByteProgress() => new SynchronousProgress<ByteProgress>(progress =>
    {
        lock (_progressLock) _bytes += progress;
    });

    private void FlushProgress(bool includeStatus = true)
    {
        while (_fileProgress.TryDequeue(out var update))
            if (_uploadItems.TryGetValue(update.Path.Replace('\\', '/'), out var item)) item.Apply(update);
        ByteProgress bytes;
        string? message;
        lock (_progressLock) { bytes = _bytes; message = _pendingStatus; _pendingStatus = null; }
        if (includeStatus && message != null) statusText.Text = message;
        if (bytes.TotalBytes > 0)
        {
            transferBar.IsIndeterminate = false;
            transferBar.Value = Math.Min(100, bytes.GetRatio() * 100);
            transferText.Text = $"{bytes.GetRatio():P0} · {FormatSize(bytes.ProgressedBytes)} / {FormatSize(bytes.TotalBytes)} (전송량)";
        }
    }

    private void SetEnabled()
    {
        loginPanel.IsEnabled = !_busy;
        btnCompare.IsEnabled = !_busy;
        UpdateRefreshButton();
        btnCancel.IsVisible = _busy;
        btnCancel.IsEnabled = true;
    }

    private void UpdateRefreshButton()
    {
        var refreshing = _refreshingUsage || _refreshingList;
        btnUsage.IsEnabled = !_busy && !refreshing && !_closed;
        btnUsage.Content = refreshing ? "새로고침 중..." : "새로고침";
    }

    private async void Next_Click(object? sender, RoutedEventArgs e)
    {
        var username = txtUsername.Text?.Trim() ?? "";
        var password = txtPassword.Text ?? "";
        var bucket = txtBucketId.Text?.Trim() ?? "";
        if (username.Length == 0 || password.Length == 0 || bucket.Length == 0)
        {
            statusText.Text = "아이디, 비밀번호, 버킷 ID를 모두 입력해 주세요.";
            return;
        }
        await Run("로그인하고 버킷을 확인하는 중…", async ct =>
        {
            var host = txtHost.Text?.Trim().TrimEnd('/') ?? "";
            _session = new(host, bucket, _applicationDirectory, _http);
            await _session.Login(username, password, ct);
            await _session.ReadRemote(ct);
            var config = _config.Config;
            config.Username = username;
            config.BucketId = bucket;
            config.Host = host;
            config.Root = _session.Workspace.Root;
            config.Token = null;
            await _config.SaveConfig();
            statusText.Text = "서버 파일을 비교하고 내려받는 중…";
            await Task.Run(() => _session.Pull(StatusProgress(), ByteProgress(), ct), ct);
            ct.ThrowIfCancellationRequested();
            txtPassword.Text = "";
            _ready = true;
            loginPanel.IsVisible = false;
            workspacePanel.IsVisible = true;
            btnFolder.IsVisible = true;
            subtitle.Text = $"{bucket} · {username}";
            pathText.Text = _session.Workspace.Root;
            StartWatching();
            _listTimer.Start();
            _usageTimer.Start();
            await UpdateList(ct);
            await UpdateUsage(ct);
            statusText.Text = "";
            OpenFolder();
        });
    }

    private void StartWatching()
    {
        _watcher?.Dispose();
        try
        {
            _watcher = new FileSystemWatcher(_session!.Workspace.Root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 32768
            };
            _watcher.Created += OnFileEvent;
            _watcher.Deleted += OnFileEvent;
            _watcher.Changed += OnFileEvent;
            _watcher.Renamed += OnFileEvent;
            _watcher.Error += (_, _) => Dispatcher.UIThread.Post(() =>
            {
                if (_closed) return;
                _watcherFailed = true;
                QueueRefresh();
            });
            _watcher.EnableRaisingEvents = true;
            _watcherFailed = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _watcherFailed = true;
            listStatus.Text = "파일 감시를 시작하지 못해 5초 주기로 갱신합니다.";
        }
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e) => Dispatcher.UIThread.Post(QueueRefresh);

    private void QueueRefresh()
    {
        if (_closed) return;
        if (!_busy) MarkStale();
        _eventTimer.Stop();
        _eventTimer.Start();
    }

    private void MarkStale()
    {
        if (_preview == null) return;
        previewSummary.Text = "파일 상태가 변경되었거나 작업이 중단되었습니다. 다시 동기화해 주세요.";
    }

    private async Task RefreshList()
    {
        if (!_ready || _busy || _closed || !_gate.Wait(0)) return;
        try
        {
            await UpdateList(_lifetime.Token);
            if (_watcherFailed) StartWatching();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            listStatus.Text = $"파일 목록 갱신 실패 · {ex.Message} · 5초 후 재시도";
            MarkStale();
        }
        finally { _gate.Release(); }
    }

    private async Task UpdateList(CancellationToken ct)
    {
        _listTimer.Stop();
        _refreshingList = true;
        UpdateRefreshButton();
        try
        {
            var local = await Task.Run(() => _session!.Workspace.Scan(ct), ct);
            ct.ThrowIfCancellationRequested();
            _localBytes = local.Where(x => !x.IsDirectory).Sum(x => x.Size);
            _localFileCount = local.Count(x => !x.IsDirectory);
            UpdateUsageBars();
            var stamp = string.Join('\n', local.OrderBy(x => x.Path, WorkspaceFiles.Comparer)
                .Select(x => $"{x.Path}:{x.Size}:{x.Checksum}:{x.IsDirectory}"));
            if (_localStamp != null && _localStamp != stamp) MarkStale();
            _localStamp = stamp;
            WorkspaceFileItem.Reconcile(_files, local, _session!.Remote);
            var changes = WorkspaceFiles.Compare(local, _session.Remote);
            addedCount.Text = $"추가 {changes.Count(x => x.Kind == "추가"):N0}";
            deletedCount.Text = $"삭제 {changes.Count(x => x.Kind == "삭제"):N0}";
            updatedCount.Text = $"갱신 {changes.Count(x => x.Kind == "갱신"):N0}";
            listStatus.Text = $"{local.Count(x => !x.IsDirectory):N0}개 파일 · {DateTime.Now:HH:mm:ss} 갱신";
        }
        finally
        {
            _refreshingList = false;
            UpdateRefreshButton();
            if (_ready && !_closed) _listTimer.Start();
        }
    }

    private void UpdateUsageBars()
    {
        if (_usage is not { } usage) return;
        var limit = usage.Limits;
        var bytes = _localBytes;
        var count = _localFileCount;
        var sizeExceeded = limit.MaxBucketSize > 0 && bytes > limit.MaxBucketSize;
        var countExceeded = limit.MaxNumberOfFiles > 0 && count > limit.MaxNumberOfFiles;
        usageText.Text = limit.MaxBucketSize > 0
            ? $"{FormatSize(bytes)} / {FormatSize(limit.MaxBucketSize)} ({(double)bytes / limit.MaxBucketSize:P0})"
            : $"{FormatSize(bytes)} · 제한값 {limit.MaxBucketSize}";
        fileCountText.Text = limit.MaxNumberOfFiles > 0
            ? $"{count:N0} / {limit.MaxNumberOfFiles:N0}개 ({(double)count / limit.MaxNumberOfFiles:P0})"
            : $"{count:N0}개 · 제한값 {limit.MaxNumberOfFiles}";
        if (sizeExceeded) usageText.Text += " · 로컬 초과";
        if (countExceeded) fileCountText.Text += " · 로컬 초과";
        usageBar.Value = limit.MaxBucketSize > 0 ? Math.Min(100, 100d * bytes / limit.MaxBucketSize) : 0;
        fileCountBar.Value = limit.MaxNumberOfFiles > 0 ? Math.Min(100, 100d * count / limit.MaxNumberOfFiles) : 0;
        SetExceeded(usageBar, sizeExceeded);
        SetExceeded(fileCountBar, countExceeded);

        static void SetExceeded(ProgressBar bar, bool exceeded)
        {
            if (exceeded) bar.Foreground = Brushes.Red;
            else bar.ClearValue(ProgressBar.ForegroundProperty);
        }
    }

    private async Task RefreshUsage()
    {
        if (!_ready || _busy || _closed || !_gate.Wait(0)) return;
        _refreshingUsage = true;
        UpdateRefreshButton();
        try { await UpdateUsage(_lifetime.Token); await UpdateList(_lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { usageStatus.Text = $"조회 실패: {ex.Message}"; }
        finally
        {
            _gate.Release();
            _refreshingUsage = false;
            UpdateRefreshButton();
        }
    }

    private async Task UpdateUsage(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(20));
            var usage = await _session!.ReadUsage(timeout.Token);
            var stamp = string.Join('\n', _session.Remote.OrderBy(x => x.Path, WorkspaceFiles.Comparer)
                .Select(x => $"{x.Path}:{x.Metadata.Size}:{x.Metadata.Checksum}"));
            if (_remoteStamp != null && stamp != _remoteStamp) MarkStale();
            _remoteStamp = stamp;
            var limit = usage.Limits;
            _usage = usage;
            UpdateUsageBars();
            quotaText.Text = limit.MonthlyMaxSyncCount >= 0
                ? $"{Math.Max(0, limit.MonthlyMaxSyncCount - usage.MonthlySyncCount):N0}회 남음 / {limit.MonthlyMaxSyncCount:N0}회"
                : $"{usage.MonthlySyncCount:N0}회 사용 · 제한값 {limit.MonthlyMaxSyncCount}";
            expiryText.Text = limit.ExpiredAt == default ? "만료일 정보 없음" :
                $"{(limit.ExpiredAt <= DateTimeOffset.UtcNow ? "만료됨" : $"{Math.Ceiling((limit.ExpiredAt - DateTimeOffset.UtcNow).TotalDays):N0}일 남음")} · {limit.ExpiredAt.ToLocalTime():yyyy-MM-dd}";
            usageStatus.Text = $"{(limit.IsReadOnly ? "읽기 전용" : "읽기·쓰기")} · {usage.FileCount:N0}개 파일";
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            usageStatus.Text = $"조회 실패: {(ex is OperationCanceledException ? "응답 시간 초과" : ex.Message)}";
        }
    }

    private async void Compare_Click(object? sender, RoutedEventArgs e) => await Run("최신 로컬 파일과 서버 파일을 비교하는 중…", async ct =>
    {
        _preview = null;
        workspacePanel.IsVisible = false;
        previewPanel.IsVisible = true;
        changesGrid.ItemsSource = null;
        previewSummary.Text = "변경사항 비교 중…";
        var comparison = await _session!.Compare(ct);
        await UpdateList(ct);
        ShowPreview(comparison);
        if (comparison.Changes.Count == 0)
        {
            statusText.Text = "동기화할 변경사항이 없습니다.";
            return;
        }
        statusText.Text = "서버 업로드 중…";
        await Upload(comparison, ct);
    });

    private void ShowPreview(Comparison comparison)
    {
        _preview = comparison;
        workspacePanel.IsVisible = false;
        previewPanel.IsVisible = true;
        var items = comparison.Changes.Select(x => new UploadFileItem(x)).ToList();
        _uploadItems = items.ToDictionary(x => x.Path, WorkspaceFiles.Comparer);
        changesGrid.ItemsSource = items;
        previewSummary.Text = $"추가 {comparison.Changes.Count(x => x.Kind == "추가")}개 · " +
            $"삭제 {comparison.Changes.Count(x => x.Kind == "삭제")}개 · 갱신 {comparison.Changes.Count(x => x.Kind == "갱신")}개. " +
            "삭제 항목은 서버에서도 삭제됩니다.";
    }

    private async Task Upload(Comparison latest, CancellationToken ct)
    {
        var usage = await _session!.ReadUsage(ct);
        _usage = usage;
        _localBytes = latest.Local.Where(x => !x.IsDirectory).Sum(x => x.Size);
        _localFileCount = latest.Local.Count(x => !x.IsDirectory);
        UpdateUsageBars();
        if (usage.Limits.IsReadOnly) throw new IOException("읽기 전용 버킷입니다.");
        if (usage.Limits.ExpiredAt != default && usage.Limits.ExpiredAt <= DateTimeOffset.UtcNow)
            throw new IOException("버킷 사용 기간이 만료되었습니다.");
        // Positive limits have unambiguous meaning; the server validates special values.
        if (usage.Limits.MonthlyMaxSyncCount > 0 && usage.MonthlySyncCount >= usage.Limits.MonthlyMaxSyncCount)
            throw new IOException("이번 달 동기화 횟수를 모두 사용했습니다.");
        if (usage.Limits.MaxBucketSize > 0 && latest.Local.Where(x => !x.IsDirectory).Sum(x => x.Size) > usage.Limits.MaxBucketSize)
            throw new IOException("동기화할 파일의 총 용량이 버킷 제한을 초과합니다.");
        if (usage.Limits.MaxFileSize > 0 && latest.Local.Any(x => !x.IsDirectory && x.Size > usage.Limits.MaxFileSize))
            throw new IOException("파일 하나의 크기가 버킷의 파일당 용량 제한을 초과합니다.");
        if (usage.Limits.MaxNumberOfFiles > 0 && latest.Local.Count(x => !x.IsDirectory) > usage.Limits.MaxNumberOfFiles)
            throw new IOException("파일 수가 버킷 제한을 초과합니다.");
        var patterns = _config.Config.WarningPatterns.Select(DotNet.Globbing.Glob.Parse).ToArray();
        var forbidden = latest.Local.FirstOrDefault(x => !x.IsDirectory && patterns.Any(p => p.IsMatch(x.Path)));
        if (forbidden != null)
        {
            var proceed = await _confirm($"동기화 주의 대상 파일이 있습니다.\n\n{forbidden.Path}\n\n그래도 동기화할까요?", "경고");
            ct.ThrowIfCancellationRequested();
            if (!proceed)
            {
                statusText.Text = "동기화를 취소했습니다.";
                return;
            }
        }
        var queue = _fileProgress;
        var fileProgress = new SynchronousProgress<FileUploadProgress>(queue.Enqueue);
        var result = await Task.Run(() => _session.Push(latest, StatusProgress(), ByteProgress(), ct, fileProgress), ct);
        lock (_progressLock) _pendingStatus = null;
        _preview = null;
        previewPanel.IsVisible = false;
        workspacePanel.IsVisible = true;
        statusText.Text = "서버 업로드 완료.";
        postUploadNotice.Text = result.VerificationWarning;
        postUploadNotice.IsVisible = result.VerificationWarning != null;
        // These are display refreshes after a confirmed server commit. A locked local
        // file or a cancelled refresh must not be reported as an upload failure.
        await RefreshAfterUpload(ct);
    }

    private async Task RefreshAfterUpload(CancellationToken ct)
    {
        try { await UpdateList(ct); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or OperationCanceledException)
        {
            listStatus.Text = "로컬 파일 목록을 갱신하지 못했습니다. 자동으로 다시 확인합니다.";
        }
        try { await UpdateUsage(ct); }
        catch (OperationCanceledException)
        {
            usageStatus.Text = "상태 조회가 취소되었습니다. 새로고침해 주세요.";
        }
    }

    private async void RefreshUsage_Click(object? sender, RoutedEventArgs e) => await RefreshUsage();
    private void Cancel_Click(object? sender, RoutedEventArgs e)
    {
        _operation?.Cancel();
        btnCancel.IsEnabled = false;
        statusText.Text = "작업을 취소하는 중…";
    }
    private void OpenFolder_Click(object? sender, RoutedEventArgs e) => OpenFolder();

    private void OpenFolder()
    {
        try
        {
            var path = _session!.Workspace.Root;
            WorkspaceFiles.EnsureNoLinks(path);
            if (_folderOpener != null) { _folderOpener(path); return; }
            var start = new ProcessStartInfo
            {
                FileName = OperatingSystem.IsWindows() ? "explorer.exe" : OperatingSystem.IsMacOS() ? "open" : "xdg-open",
                UseShellExecute = false
            };
            start.ArgumentList.Add(path);
            Process.Start(start);
        }
        catch (Exception ex) { statusText.Text = $"폴더를 열지 못했습니다: {ex.Message}. 경로: {_session?.Workspace.Root}"; }
    }

    public static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var index = 0;
        while (value >= 1024 && index < units.Length - 1) { value /= 1024; index++; }
        return $"{value:0.##} {units[index]}";
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_busy)
        {
            e.Cancel = true;
            _operation?.Cancel();
            statusText.Text = "작업을 취소하는 중입니다. 정리가 끝나면 창을 닫아 주세요.";
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closed = true;
        _lifetime.Cancel();
        _watcher?.Dispose();
        _listTimer.Stop();
        _usageTimer.Stop();
        _eventTimer.Stop();
        _progressTimer.Stop();
        base.OnClosed(e);
    }
}
