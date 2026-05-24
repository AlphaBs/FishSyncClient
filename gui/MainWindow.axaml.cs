using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FishBucket;
using FishBucket.ApiClient;
using FishBucket.SyncClient;
using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Syncer;
using gui;
using System.Diagnostics;
using System.IO;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace FishSyncClient.Gui;

public partial class MainWindow : Window
{
    private readonly PathOptions _pathOptions = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    bool needSync = true;
    bool _configSaved = false;
    ConfigManager configManager = ConfigManager.Instance;
    CancellationTokenSource cancellationTokenSource = new();

    // Log lines arrive at high frequency (one per file event) and from multiple threads.
    // Buffer them and flush to the UI on a timer instead of mutating the text box per line,
    // which would re-layout the whole (ever-growing) string on every append.
    private readonly object _logLock = new();
    private readonly StringBuilder _logBuffer = new();
    private bool _logDirty;
    private DispatcherTimer? _logTimer;
    private const int MaxLogLength = 100_000;

    private async void Window_Loaded(object? sender, RoutedEventArgs e)
    {
        Logger.Instance.Append += (s, ev) => appendLog(ev);

        _logTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _logTimer.Tick += (_, _) => flushLogs();
        _logTimer.Start();

        await ConfigManager.Instance.LoadConfig();

        await checkUpdate();

        txtHost.Text = configManager.Config.Host;
        txtRoot.Text = configManager.Config.Root;
        txtBucketId.Text = configManager.Config.BucketId;

        sourceSyncFiles.CollectionName = "로컬";
        targetSyncFiles.CollectionName = "서버";
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);

        if (_configSaved)
            return;

        // Defer the close until the configuration has been persisted.
        e.Cancel = true;

        configManager.Config.Host = txtHost.Text;
        configManager.Config.Root = txtRoot.Text;
        configManager.Config.BucketId = txtBucketId.Text;

        await configManager.SaveConfig();

        _configSaved = true;
        Close();
    }

    private FishApiClient createApiClient()
    {
        var apiClient = new FishApiClient(txtHost.Text ?? "", HttpUtil.HttpClient);
        apiClient.ApiKey = configManager.Config.Token;
        return apiClient;
    }

    private void appendLog(string message)
    {
        lock (_logLock)
        {
            _logBuffer.Append(message).Append('\n');
            _logDirty = true;
        }
    }

    private void flushLogs()
    {
        string pending;
        lock (_logLock)
        {
            if (!_logDirty)
                return;

            pending = _logBuffer.ToString();
            _logBuffer.Clear();
            _logDirty = false;
        }

        var text = (txtLogs.Text ?? "") + pending;
        if (text.Length > MaxLogLength)
            text = text.Substring(text.Length - MaxLogLength);

        txtLogs.Text = text;

        if (cbScrollLog.IsChecked ?? false)
            logScroll.ScrollToEnd();
    }

    private void cbScrollLog_Checked(object? sender, RoutedEventArgs e)
    {
        logScroll.ScrollToEnd();
    }

    private async void btnOpen_Click(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            AllowMultiple = false
        });

        if (folders.Count > 0)
            txtRoot.Text = folders[0].Path.LocalPath;
    }

    private async Task loadFiles(CancellationToken cancellationToken)
    {
        var root = txtRoot.Text ?? "";
        var host = txtHost.Text ?? "";
        var id = txtBucketId.Text ?? "";

        var sourceTask = loadSource(root, cancellationToken);
        var targetTask = loadTarget(host, id, cancellationToken);

        await Task.WhenAll(sourceTask, targetTask);
        await sourceTask; // get exception
        await targetTask; // get exception
    }

    private async Task loadSource(string root, CancellationToken cancellationToken)
    {
        sourceSyncFiles.ClearProgress();
        sourceSyncFiles.Clear();
        await Task.Run(() =>
        {
            var files = RootedPath.FromDirectory(root, new PathOptions()).Select(createLocalSyncFile);
            addFiles(root, files, sourceSyncFiles, cancellationToken);
        });
    }

    private async Task loadTarget(string host, string id, CancellationToken cancellationToken)
    {
        targetSyncFiles.ClearProgress();
        targetSyncFiles.Clear();
        var apiClient = createApiClient();
        var files = await apiClient.GetBucketFiles(id, cancellationToken);
        var syncFiles = files.Files.Select(createFishSyncFile);
        addFiles(host, syncFiles, targetSyncFiles, cancellationToken);
    }

    private SyncFile createLocalSyncFile(RootedPath path)
    {
        var fileInfo = new FileInfo(path.GetFullPath());
        using var fs = File.OpenRead(fileInfo.FullName);
        var checksum = ChecksumAlgorithms.ComputeMD5(fs);
        return new LocalSyncFile(path)
        {
            Metadata = new SyncFileMetadata()
            {
                Size = fileInfo.Length,
                Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.MD5, checksum)
            }
        };
    }

    private SyncFile createFishSyncFile(BucketFile file)
    {
        if (string.IsNullOrEmpty(file.Path) || string.IsNullOrEmpty(file.Location))
            throw new ArgumentException();

        var path = RootedPath.FromSubPath(file.Path, _pathOptions);
        return new ReadableHttpSyncFile(path, HttpUtil.HttpClient)
        {
            Location = new Uri(file.Location),
            Metadata = new SyncFileMetadata
            {
                Size = file.Metadata.Size,
                Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.MD5, file.Metadata.Checksum)
            }
        };
    }

    private void addFiles(string name, IEnumerable<SyncFile> files, SyncFileCollectionControl control, CancellationToken cancellationToken)
    {
        Logger.Instance.LogInformation($"[로드] {name}");

        foreach (var file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Dispatcher.UIThread.Invoke(() =>
            {
                control.Add(file);
            });
        }

        Logger.Instance.LogInformation($"[로드] {name} 불러오기 완료. 총 갯수 {control.TotalFiles}, 용량 {control.TotalBytes:##,#} bytes");
    }

    private async void btnCompare_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            setUIEnables(false);

            cancellationTokenSource = new();
            var cancellationToken = cancellationTokenSource.Token;
            await loadFiles(cancellationToken);

            var sources = sourceSyncFiles.GetFiles();
            var targets = targetSyncFiles.GetFiles();

            var syncer = new SyncFileCollectionSyncer(
                new ParallelSyncFilePairSyncer(),
                new PathOptions() { CaseInsensitive = true });
            var result = await syncer.CompareFiles(
                sources,
                targets,
                new FileChecksumMetadataComparer(),
                new SyncerOptions());

            foreach (var identical in result.IdenticalFilePairs)
            {
                sourceSyncFiles.SetStatus(identical.Source, "동일");
                targetSyncFiles.SetStatus(identical.Target, "동일");
            }

            foreach (var updated in result.UpdatedFilePairs)
            {
                sourceSyncFiles.SetStatus(updated.Source, "업데이트");
                targetSyncFiles.SetStatus(updated.Target, "업데이트");
            }

            foreach (var added in result.AddedFiles)
            {
                sourceSyncFiles.SetStatus(added, "로컬");
            }

            foreach (var deleted in result.DeletedFiles)
            {
                targetSyncFiles.SetStatus(deleted, "서버");
            }

            needSync = result.UpdatedFilePairs.Any() || result.AddedFiles.Any() || result.DeletedFiles.Any();

            await MessageBox.Show($"비교 결과: \n" +
                $"동일한 파일 {result.IdenticalFilePairs.Count}개\n" +
                $"바뀐 파일 {result.UpdatedFilePairs.Count}개\n" +
                $"로컬 파일 {result.AddedFiles.Count}개\n" +
                $"서버 파일 {result.DeletedFiles.Count}개");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError("[비교] 예외 발생 " + ex.ToString());
            await MessageBox.Show(ex.ToString());
        }
        finally
        {
            setUIEnables(true);
        }
    }

    private async void btnPull_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            setUIEnables(false);

            cancellationTokenSource = new();
            var cancellationToken = cancellationTokenSource.Token;
            Logger.Instance.LogInformation($"[PULL] {targetSyncFiles.Count} items");

            var fileProgress = new Progress<FileProgressEvent>(ev =>
            {
                Logger.Instance.LogInformation($"[PULL] {ev.EventType}: {ev.CurrentFileName}");
                targetSyncFiles.SetStatus(ev.CurrentFileName, ev.EventType);
            });
            // High-frequency byte progress is accumulated off the UI thread and flushed in
            // batches by the control's timer, so it must NOT go through Progress<T>.
            var byteProgress = new SynchronousProgress<SyncFileByteProgress>(ev =>
            {
                targetSyncFiles.AddProgress(ev.SyncFile, ev.Progress);
            });

            var sourceFiles = sourceSyncFiles.GetFiles().ToArray();
            var targetFiles = targetSyncFiles.GetFiles().ToArray();

            var syncer = new LocalSyncer(
                txtRoot.Text ?? "",
                new PathOptions(),
                new ParallelSyncFilePairSyncer());
            var syncResult = await syncer.CompareAndSyncFiles(
                targetFiles,
                sourceFiles,
                new LocalFileChecksumComparer(),
                new SyncerOptions
                {
                    FileProgress = fileProgress,
                    ByteProgress = byteProgress,
                    CancellationToken = cancellationToken
                });

            Logger.Instance.LogInformation($"[PULL] 완료: " +
                $"업데이트 {syncResult.UpdatedFilePairs.Count} 개, " +
                $"추가 {syncResult.AddedFiles.Count} 개, " +
                $"삭제 {syncResult.DeletedFiles.Count} 개, " +
                $"동일한 파일 {syncResult.IdenticalFilePairs.Count} 개");
            await MessageBox.Show($"PULL 성공");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"[PULL] 예외 발생: {ex}");
            await MessageBox.Show(ex.ToString());
        }
        finally
        {
            setUIEnables(true);
            btnCompare_Click(this, e);
        }
    }

    private async void btnPush_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            setUIEnables(false);

            if (!needSync)
            {
                var mbResult = await MessageBox.Show(
                    "경고: 모든 파일이 서버와 동일하여 동기화가 필요 없습니다.\n\n" +
                    "동기화 횟수가 차감됩니다. 그래도 동기화를 시도할까요?",
                    "경고",
                    MessageBoxButtons.YesNo);

                if (mbResult == MessageBoxResult.No)
                    return;
            }

            var patterns = configManager.Config.WarningPatterns
                .Select(DotNet.Globbing.Glob.Parse)
                .ToList();
            var warningFilePath = sourceSyncFiles.GetFiles()
                .Select(file => file.Path.SubPath)
                .Where(path => patterns.Any(pattern => pattern.IsMatch(path)))
                .FirstOrDefault();
            if (warningFilePath != null)
            {
                var warningPattern = patterns.First(pattern => pattern.IsMatch(warningFilePath));
                var mbResult = await MessageBox.Show(
                    "경고: 아래 파일은 동기화가 금지되어 있습니다.\n\n" +
                    $"파일: {warningFilePath}\n" +
                    $"패턴: {warningPattern}\n\n" +
                    "동기화를 시도할 경우 실패할 수 있습니다. 그래도 동기화를 시도할까요?",
                    "경고",
                    MessageBoxButtons.YesNo);

                if (mbResult == MessageBoxResult.No)
                    return;
            }

            cancellationTokenSource = new();
            var cancellationToken = cancellationTokenSource.Token;
            Logger.Instance.LogInformation($"[PUSH] {sourceSyncFiles.Count} items");

            var actionProgress = new Progress<SyncActionProgress>(ev =>
            {
                Logger.Instance.LogInformation($"[PUSH] BucketSyncAction {ev.EventType}: {ev.Action.Action.Type}, {ev.Action.Path}");
                sourceSyncFiles.SetStatus(ev.Action.Path, ev.EventType);
            });
            // See PULL: byte progress is batched off the UI thread, not marshalled per report.
            var byteProgress = new SynchronousProgress<SyncActionByteProgress>(ev =>
            {
                sourceSyncFiles.AddProgress(ev.Path, ev.Progress);
            });

            var handler = new SimpleBucketSyncActionCollectionHandler(6, actionProgress, byteProgress);
            handler.Add(new HttpBucketSyncActionHandler(HttpUtil.HttpClient));

            var apiClient = createApiClient();
            var result = await apiClient.Sync(txtBucketId.Text ?? "", sourceSyncFiles, handler, cancellationToken);

            if (result.IsSuccess)
                await MessageBox.Show($"PUSH 성공, UpdatedAt {result.UpdatedAt}");
            else
                await MessageBox.Show("PUSH 실패\n" + string.Join("\n", result.RequiredActions));

        }
        catch (ActionRequiredException actionRequiredException)
        {
            foreach (var action in actionRequiredException.Actions)
            {
                Logger.Instance.LogError($"[PUSH] 처리할 수 없는 SyncAction: {action.Path}, {action.Action.Type}, {JsonSerializer.Serialize(action.Action.Parameters)}");
            }
            var errorMessage = string.Join('\n', actionRequiredException.Actions.Select(action => $"{action.Path}: {action.Action.Type}"));
            await MessageBox.Show("처리할 수 없는 작업이 있습니다: \n" + errorMessage);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError("[PUSH] 예외 발생 " + ex.ToString());
            await MessageBox.Show(ex.ToString());
        }
        finally
        {
            setUIEnables(true);
            btnCompare_Click(this, e);
        }
    }

    private void setUIEnables(bool value)
    {
        txtHost.IsEnabled = value;
        txtRoot.IsEnabled = value;
        btnOpen.IsEnabled = value;
        btnFishLogin.IsEnabled = value;
        btnCompare.IsEnabled = value;
        btnPull.IsEnabled = value;
        btnPush.IsEnabled = value;

        btnCancel.IsVisible = !value;
    }

    private async void btnFishLogin_Click(object? sender, RoutedEventArgs e)
    {
        var apiClient = createApiClient();
        var loginWindow = new LoginWindow(apiClient);
        await loginWindow.ShowDialog(this);
    }

    private void btnCancel_Click(object? sender, RoutedEventArgs e)
    {
        cancellationTokenSource.Cancel();
    }

    private void btnOpenWeb_Click(object? sender, RoutedEventArgs e)
    {
        OpenUrl($"https://fish.alphabeta.pw/Web/Buckets/List?id={txtBucketId.Text}&handler=RedirectToBucket");
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(url);
        }
        catch
        {
            // hack because of this: https://github.com/dotnet/corefx/issues/10361
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
            else
            {
                throw;
            }
        }
    }

    private async void btnCheckUpdate_Click(object? sender, RoutedEventArgs e)
    {
        await checkUpdate();
    }

    private async Task checkUpdate()
    {
        try
        {
            var version = await HttpUtil.HttpClient.GetFromJsonAsync<UpdateVersion>("https://alphabeta.pw/home/shares/fish/win-x64/version.json");
            if (string.IsNullOrEmpty(version?.Version))
                throw new FormatException("version?.Version was null or empty");

            var currentVersion = configManager.Config.ClientVersion;
            if (version.Version == currentVersion)
            {
                await MessageBox.Show($"FISH 동기화 클라이언트\n버전: {currentVersion}\n최신 버전입니다.");
            }
            else
            {
                var dialogResult = await MessageBox.Show($"새로운 업데이트가 있습니다.\n\n" +
                    $"현재 버전: {currentVersion}\n" +
                    $"최신 버전: {version.Version}\n\n" +
                    $"최신 버전을 다운로드 할까요?",
                    "업데이트",
                    MessageBoxButtons.YesNo);

                if (dialogResult == MessageBoxResult.Yes)
                {
                    OpenUrl(version.Download ?? "https://fish.alphabeta.pw/Web/Home");
                }
            }
        }
        catch (Exception ex)
        {
            await MessageBox.Show(ex.ToString());
            Environment.Exit(-1);
        }
    }
}
