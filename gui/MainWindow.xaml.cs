using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Progress;
using FishSyncClient.Server;
using FishSyncClient.Server.BucketSyncActions;
using FishSyncClient.Syncer;
using FishSyncClient.Versions;
using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;

namespace FishSyncClient.Gui;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly HttpClient _httpClient;
    private readonly PathOptions _pathOptions = new();

    public MainWindow()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromHours(1);
        InitializeComponent();
    }

    ConfigManager configManager = new("config.json");
    CancellationTokenSource cancellationTokenSource = new();

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Instance.Append += (s, e) => appendLog(e);
        await configManager.LoadConfig();

        txtHost.Text = configManager.Config.Host;
        txtRoot.Text = configManager.Config.Root;
        txtBucketId.Text = configManager.Config.BucketId;

        sourceSyncFiles.CollectionName = "로컬";
        targetSyncFiles.CollectionName = "서버";
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        configManager.Config.Host = txtHost.Text;
        configManager.Config.Root = txtRoot.Text;
        configManager.Config.BucketId = txtBucketId.Text;

        await configManager.SaveConfig();
    }

    private void appendLog(string message)
    {
        Dispatcher.Invoke(() =>
        {
            txtLogs.AppendText(message);
            txtLogs.AppendText("\n");

            if (cbScrollLog.IsChecked ?? false)
                txtLogs.ScrollToEnd();
        });
    }

    private void cbScrollLog_Checked(object sender, RoutedEventArgs e)
    {
        txtLogs.ScrollToEnd();
    }

    private void btnOpen_Click(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog();
        folderDialog.Multiselect = false;
        folderDialog.ShowDialog();
        txtRoot.Text = folderDialog.FolderName;
    }

    private async Task loadFiles(CancellationToken cancellationToken)
    {
        var root = txtRoot.Text;
        var host = txtHost.Text;
        var id = txtBucketId.Text;

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
        var apiClient = new FishApiClient(host, _httpClient);
        var files = await apiClient.GetBucketFiles(id, cancellationToken);
        var syncFiles = files.Files?.Select(createFishSyncFile) ?? [];
        addFiles(host, syncFiles, targetSyncFiles, cancellationToken);
    }

    private SyncFile createLocalSyncFile(RootedPath path)
    {
        var fileinfo = new FileInfo(path.GetFullPath());
        using var fs = File.OpenRead(fileinfo.FullName);
        var checksum = ChecksumAlgorithms.ComputeMD5(fs);
        return new LocalSyncFile(path)
        {
            Metadata = new SyncFileMetadata()
            {
                Size = fileinfo.Length,
                Checksum = new SyncFileChecksum(ChecksumAlgorithmNames.MD5, checksum)
            }
        };
    }

    private SyncFile createFishSyncFile(FishBucketFile file)
    {
        if (string.IsNullOrEmpty(file.Path) || string.IsNullOrEmpty(file.Location))
            throw new ArgumentException();

        var path = RootedPath.FromSubPath(file.Path, _pathOptions);
        return new ReadableHttpSyncFile(path, _httpClient)
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

            Dispatcher.Invoke(() =>
            {
                control.Add(file);
            });
        }

        Logger.Instance.LogInformation($"[로드] {name} 불러오기 완료. 총 갯수 {control.TotalFiles}, 용량 {control.TotalBytes:##,#} bytes");
    }

    private async void btnCompare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            setUIEnables(false);

            cancellationTokenSource = new();
            var cancellationToken = cancellationTokenSource.Token;
            await loadFiles(cancellationToken);

            var sources = sourceSyncFiles.GetFiles();
            var targets = targetSyncFiles.GetFiles();

            var syncer = new SyncFileCollectionSyncer(new ParallelSyncFilePairSyncer());
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

            MessageBox.Show($"비교 결과: \n" +
                $"동일한 파일 {result.IdenticalFilePairs.Count}개\n" +
                $"바뀐 파일 {result.UpdatedFilePairs.Count}개\n" +
                $"로컬 파일 {result.AddedFiles.Count}개\n" +
                $"서버 파일 {result.DeletedFiles.Count}개");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError("[비교] 예외 발생 " + ex.ToString());
            MessageBox.Show(ex.ToString());
        }
        finally
        {
            setUIEnables(true);
        }
    }

    private async void btnPull_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            setUIEnables(false);

            cancellationTokenSource = new();
            var cancellationToken = cancellationTokenSource.Token;
            Logger.Instance.LogInformation($"[PULL] {targetSyncFiles.Count} items");

            var fileProgress = new Progress<FileProgressEvent>(e =>
            {
                Logger.Instance.LogInformation($"[PULL] {e.EventType}: {e.CurrentFileName}");
                targetSyncFiles.SetStatus(e.CurrentFileName, e.EventType);
            });
            var byteProgress = new Progress<SyncFileByteProgress>(e =>
            {
                targetSyncFiles.AddProgress(e.SyncFile, e.Progress);
            });

            var sourceFiles = sourceSyncFiles.GetFiles().ToArray();
            var targetFiles = targetSyncFiles.GetFiles().ToArray();

            var syncer = new LocalSyncer(
                txtRoot.Text,
                new PathOptions(),
                new ParallelSyncFilePairSyncer());
            var syncResult = await syncer.CompareAndSyncFiles(
                targetFiles,
                sourceFiles,
                new FileChecksumMetadataComparer(),
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
            MessageBox.Show($"PULL 성공");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError($"[PULL] 예외 발생: {ex}");
            MessageBox.Show(ex.ToString());
        }
        finally
        {
            setUIEnables(true);
            btnCompare_Click(this, e);
        }
    }

    private async void btnPush_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            setUIEnables(false);

            cancellationTokenSource = new();
            var cancellationToken = cancellationTokenSource.Token;
            Logger.Instance.LogInformation($"[PUSH] {sourceSyncFiles.Count} items");

            var actionProgress = new Progress<SyncActionProgress>(e =>
            {
                Logger.Instance.LogInformation($"[PUSH] BucketSyncAction {e.EventType}: {e.Action.Action.Type}, {e.Action.Path}");
                sourceSyncFiles.SetStatus(e.Action.Path, e.EventType);
            });
            var byteProgress = new Progress<SyncActionByteProgress>(e =>
            {
                sourceSyncFiles.AddProgress(e.Path, e.Progress);
            });

            var handler = new SimpleBucketSyncActionCollectionHandler(6, actionProgress, byteProgress);
            handler.Add(new HttpBucketSyncActionHandler(_httpClient));
            var apiClient = new FishApiClient(txtHost.Text, _httpClient);
            var result = await apiClient.Sync(txtBucketId.Text, sourceSyncFiles, handler, cancellationToken);

            if (result.IsSuccess)
                MessageBox.Show($"PUSH 성공, UpdatedAt {result.UpdatedAt}");
            else
                MessageBox.Show("PUSH 실패\n" + string.Join("\n", result.Actions));

        }
        catch (ActionRequiredException actionRequiredException)
        {
            foreach (var action in actionRequiredException.Actions)
            {
                Logger.Instance.LogError($"[PUSH] 처리할 수 없는 SyncAction: {action.Path}, {action.Action.Type}, {JsonSerializer.Serialize(action.Action.Parameters)}");
            }
            var errorMessage = string.Join('\n', actionRequiredException.Actions.Select(action => $"{action.Path}: {action.Action.Type}"));
            MessageBox.Show("처리할 수 없는 작업이 있습니다: \n" + errorMessage);
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError("[PUSH] 예외 발생 " + ex.ToString());
            MessageBox.Show(ex.ToString());
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

        if (value)
            btnCancel.Visibility = Visibility.Collapsed;
        else
            btnCancel.Visibility = Visibility.Visible;
    }

    private void btnFishLogin_Click(object sender, RoutedEventArgs e)
    {

    }

    private void btnCancel_Click(object sender, RoutedEventArgs e)
    {
        cancellationTokenSource.Cancel();
    }
}