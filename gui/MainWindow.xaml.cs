using FishSyncClient.FileComparers;
using FishSyncClient.Files;
using FishSyncClient.Server;
using FishSyncClient.Server.BucketSyncActions;
using FishSyncClient.Syncer;
using Microsoft.Win32;
using System;
using System.Diagnostics;
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
    private readonly IFileComparerFactory _fileComparerFactory = new DefaultFileComparerFactory();

    public MainWindow()
    {
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromHours(1);
        InitializeComponent();
    }

    ConfigManager configManager = new("config.json");
    CancellationTokenSource? cancellationTokenSource;

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Logger.Instance.Append += (s, e) => appendLog(e);
        await configManager.LoadConfig();

        txtHost.Text = configManager.Config.Host;
        txtRoot.Text = configManager.Config.Root;

        sourceSyncFiles.CollectionName = "로컬";
        targetSyncFiles.CollectionName = "서버";
    }

    private async void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        configManager.Config.Host = txtHost.Text;
        configManager.Config.Root = txtRoot.Text;

        await configManager.SaveConfig();
    }

    private void btnOpen_Click(object sender, RoutedEventArgs e)
    {
        var folderDialog = new OpenFolderDialog();
        folderDialog.Multiselect = false;
        folderDialog.ShowDialog();
        txtRoot.Text = folderDialog.FolderName;
        btnLoad_Click(this, e);
    }

    private async void btnLoad_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var root = txtRoot.Text;
            var host = txtHost.Text;

            cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = cancellationTokenSource.Token;

            setUIEnables(false);
            btnCancelLoad.Visibility = Visibility.Visible;

            sourceSyncFiles.Clear();
            targetSyncFiles.Clear();

            var sourceTask = Task.Run(() =>
            {
                var pushClient = new PushClient();
                var files = RootedPath.FromDirectory(root, new PathOptions()).Select(createLocalSyncFile);
                addFiles(root, files, sourceSyncFiles, cancellationToken);
            });

            var targetTask = async() =>
            {
                var apiClient = new FishApiClient(host, _httpClient);
                var files = await apiClient.GetBucketFiles("first", cancellationToken);
                var syncFiles = files.Files?.Select(createFishSyncFile) ?? [];
                addFiles(host, syncFiles, targetSyncFiles, cancellationToken);
            };

            await Task.WhenAll(sourceTask, targetTask());
        }
        catch (Exception ex)
        {
            Logger.Instance.LogInformation("[로드] 예외 발생 " + ex.ToString());
            MessageBox.Show(ex.ToString());
        }
        finally
        {
            setUIEnables(true);
            btnCancelLoad.Visibility = Visibility.Collapsed;
        }
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
                Checksum = checksum,
                ChecksumAlgorithm = "md5"
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
                Checksum = file.Metadata.Checksum,
                ChecksumAlgorithm = "md5"
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

    private void btnCancelLoad_Click(object sender, RoutedEventArgs e)
    {
        cancellationTokenSource?.Cancel();
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

    private async void btnCompare_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            setUIEnables(true);

            var sources = sourceSyncFiles.GetFiles();
            var targets = targetSyncFiles.GetFiles();

            var syncer = new FishSyncer(new DryFileSyncer());
            var result = await syncer.Sync(sources, targets, _fileComparerFactory.CreateFullComparer(), new SyncOptions());

            foreach (var identical in result.IdenticalFiles)
            {
                sourceSyncFiles.SetStatus(identical.Source, "동일");
                targetSyncFiles.SetStatus(identical.Target, "동일");
            }

            foreach (var updated in result.UpdatedFiles)
            {
                sourceSyncFiles.SetStatus(updated.Source, "업데이트");
                targetSyncFiles.SetStatus(updated.Target, "업데이트");
            }

            foreach (var added in result.AddedFiles)
            {
                sourceSyncFiles.SetStatus(added, "추가");
            }

            foreach (var deleted in result.DeletedFiles)
            {
                targetSyncFiles.SetStatus(deleted, "삭제");
            }

            MessageBox.Show($"비교 결과: \n" +
                $"동일한 파일 {result.IdenticalFiles.Count}개\n" +
                $"바뀐 파일 {result.UpdatedFiles.Count}개\n" +
                $"추가된 파일 {result.AddedFiles.Count}개\n" +
                $"삭제된 파일 {result.DeletedFiles.Count}개");
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

    private void cbScrollLog_Checked(object sender, RoutedEventArgs e)
    {
        txtLogs.ScrollToEnd();
    }

    private void btnPull_Click(object sender, RoutedEventArgs e)
    {

    }

    private async void btnPush_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            setUIEnables(false);
            var cancellationToken = cancellationTokenSource?.Token ?? default;

            var actionProgress = new Progress<SyncActionProgress>(e =>
            {
                Logger.Instance.LogInformation($"[PUSH] BucketSyncAction {e.EventType}: {e.Action.Action.Type}, {e.Action.Path}");
                if (e.EventType == SyncActionEventTypes.Queue)
                {
                    sourceSyncFiles.StartProgress(e.Action.Path);
                    sourceSyncFiles.SetStatus(e.Action.Path, $"업로드 대기 ({e.Action.Action.Type})");
                }
                else if (e.EventType == SyncActionEventTypes.Done)
                {
                    sourceSyncFiles.CompleteProgress(e.Action.Path);
                    sourceSyncFiles.SetStatus(e.Action.Path, $"업로드 완료 ({e.Action.Action.Type})");
                }
            });
            var byteProgress = new Progress<SyncActionByteProgress>(e =>
            {
                sourceSyncFiles.SetProgress(e.Path, e.Progress.GetPercentage(false));
            });

            var handler = new SimpleBucketSyncActionCollectionHandler(6, actionProgress, byteProgress);
            handler.Add(new HttpBucketSyncActionHandler(_httpClient));
            var apiClient = new FishApiClient(txtHost.Text, _httpClient);

            BucketSyncResult result;
            while (true)
            {
                result = await apiClient.Sync("first", sourceSyncFiles);
                Logger.Instance.LogInformation($"[PUSH] sync 요청 결과: {result.IsSuccess}, UpdatedAt {result.UpdatedAt}, {result.Actions.Count} 개 작업 필요");

                if (result.IsSuccess)
                    break;

                await handler.Handle(sourceSyncFiles, result.Actions, cancellationToken);
            }

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
        }
    }

    private void setUIEnables(bool value)
    {
        txtHost.IsEnabled = value;
        txtRoot.IsEnabled = value;
        btnOpen.IsEnabled = value;
        btnLoad.IsEnabled = value;
        btnCompare.IsEnabled = value;
        btnPull.IsEnabled = value;
        btnPush.IsEnabled = value;
    }
}