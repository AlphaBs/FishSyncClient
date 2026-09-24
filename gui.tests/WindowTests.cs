using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FishSyncClient.Gui;
using System.Collections.ObjectModel;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(FishSyncClient.Gui.Tests.TestAppBuilder))]

namespace FishSyncClient.Gui.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class WindowTests
{
    [AvaloniaFact]
    public async Task LoginPullWatchPreviewPushFlow()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer
        {
            Files = [WorkspaceTests.Remote("docs/readme.txt", "readme"), WorkspaceTests.Remote("old.txt", "old")]
        };
        server.Content["docs/readme.txt"] = "readme";
        server.Content["old.txt"] = "old";
        using var http = new HttpClient(server);
        string? opened = null;
        var config = new ConfigManager(Path.Combine(directory.Path, "config", "config.json"));
        var window = new MainWindow(directory.Path, http, config, path => opened = path);
        window.Show();
        try
        {
            await Until(() => Get<Button>("btnNext").IsEnabled && Get<TextBox>("txtHost").Text != null);
            Get<TextBox>("txtUsername").Text = "tester";
            Get<TextBox>("txtPassword").Text = "test-password";
            Get<TextBox>("txtBucketId").Text = "bucket";
            Get<TextBox>("txtHost").Text = "https://api.test/api";
            Capture(window, "login");
            Click("btnNext");
            await Until(() => Get<Grid>("workspacePanel").IsVisible && !Get<Button>("btnCancel").IsVisible);
            Assert.Equal(Path.Combine(directory.Path, "buckets", "bucket"), opened);
            Assert.Contains("17회", Get<TextBlock>("quotaText").Text);
            Assert.Contains("2 / 100", Get<TextBlock>("fileCountText").Text);
            Assert.Equal(2, Get<ProgressBar>("fileCountBar").Value);
            Assert.Equal("", Get<TextBox>("txtPassword").Text);
            Assert.Equal("tester", config.Config.Username);
            Assert.Equal(Avalonia.Styling.ThemeVariant.Light, window.ActualThemeVariant);
            var items = (ObservableCollection<WorkspaceFileItem>)Get<ListBox>("fileList").ItemsSource!;
            var readme = items.Single(x => x.Path == "docs/readme.txt");
            AssertClickKeepsBackground(window, Get<ListBox>("fileList"));
            Get<ListBox>("fileList").SelectedItem = readme;
            var root = opened!;
            await File.WriteAllTextAsync(Path.Combine(root, "new.txt"), "new");
            File.Delete(Path.Combine(root, "old.txt"));
            await File.WriteAllTextAsync(Path.Combine(root, "docs", "readme.txt"), "changed");
            await Until(() => items.Any(x => x.Kind == "추가") && items.Any(x => x.Kind == "삭제") && readme.Kind == "갱신");
            Assert.Same(readme, items.Single(x => x.Path == "docs/readme.txt"));
            Assert.Same(readme, Get<ListBox>("fileList").SelectedItem);
            Capture(window, "workspace");
            server.SyncGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Click("btnCompare");
            await Until(() => Get<Grid>("previewPanel").IsVisible && Get<ListBox>("changesGrid").ItemCount == 3);
            Assert.Equal(3, ((IEnumerable<UploadFileItem>)Get<ListBox>("changesGrid").ItemsSource!).Count());
            Assert.True(Get<Button>("btnCancel").IsVisible);
            Assert.False(Get<Button>("btnCompare").IsEnabled);
            AssertClickKeepsBackground(window, Get<ListBox>("changesGrid"));
            Capture(window, "preview");
            server.SyncGate.SetResult();
            await Until(() => Get<Grid>("workspacePanel").IsVisible && !Get<Button>("btnCancel").IsVisible);
            Assert.Equal(1, server.SyncCalls);
            Assert.DoesNotContain(server.Files, x => x.Path == "old.txt");
            Assert.Equal("서버 업로드 완료.", Get<TextBlock>("statusText").Text);
            Assert.DoesNotContain(items, x => x.Kind == "삭제");
            Click("btnCompare");
            await Until(() => !Get<Button>("btnCancel").IsVisible);
            Assert.Equal(1, server.SyncCalls);
            Assert.Equal("동기화할 변경사항이 없습니다.", Get<TextBlock>("statusText").Text);
            Assert.True(Get<Grid>("workspacePanel").IsVisible);
            Assert.False(Get<Grid>("previewPanel").IsVisible);

            // With watcher events disabled, the five-second timer still discovers changes.
            var watcherField = typeof(MainWindow).GetField("_watcher", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
            ((FileSystemWatcher)watcherField.GetValue(window)!).EnableRaisingEvents = false;
            await Task.Delay(450); // drain queued watcher notifications before this independent check
            await Task.Delay(3000);
            Click("btnUsage");
            await Until(() => Get<Button>("btnUsage").IsEnabled);
            await File.WriteAllTextAsync(Path.Combine(root, "timer-only.txt"), "timer");
            await Task.Delay(3000);
            Assert.DoesNotContain(items, x => x.Path == "timer-only.txt");
            await Until(() => items.Any(x => x.Path == "timer-only.txt"), TimeSpan.FromSeconds(8));
            Assert.Contains("3 / 100", Get<TextBlock>("fileCountText").Text);
            Assert.Equal(3, Get<ProgressBar>("fileCountBar").Value);
            Assert.StartsWith("15 B /", Get<TextBlock>("usageText").Text);

            // A failed quota refresh keeps the working tree usable and retains the last values.
            server.FailUsage = true;
            Click("btnUsage");
            await Until(() => Get<TextBlock>("usageStatus").Text!.Contains("조회 실패"));
            Assert.Contains("17회", Get<TextBlock>("quotaText").Text);
            Assert.True(Get<Button>("btnCompare").IsEnabled);
        }
        finally { window.Close(); }

        T Get<T>(string name) where T : Control => window.FindControl<T>(name)!;
        void Click(string name) => Get<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    [AvaloniaFact]
    public async Task LimitsBlockUploadAndLockedPostCommitFileStillShowsSuccess()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer();
        using var http = new HttpClient(server);
        var config = new ConfigManager(Path.Combine(directory.Path, "config.json"));
        var window = new MainWindow(directory.Path, http, config, _ => { });
        FileStream? fileLock = null;
        window.Show();
        try
        {
            await Until(() => Get<Button>("btnNext").IsEnabled);
            Get<TextBox>("txtUsername").Text = "tester";
            Get<TextBox>("txtPassword").Text = "password";
            Get<TextBox>("txtBucketId").Text = "bucket";
            Get<TextBox>("txtHost").Text = "https://api.test/api";
            Click("btnNext");
            await Until(() => Get<Grid>("workspacePanel").IsVisible && !Get<Button>("btnCancel").IsVisible);
            var root = Path.Combine(directory.Path, "buckets", "bucket");
            await File.WriteAllTextAsync(Path.Combine(root, "one.txt"), "abc");
            await File.WriteAllTextAsync(Path.Combine(root, "two.txt"), "def");

            server.Limits = server.Limits with { MaxNumberOfFiles = 1 };
            Click("btnCompare");
            await Until(() => !Get<Button>("btnCancel").IsVisible);
            Assert.Contains("파일 수가 버킷 제한을 초과", Get<TextBlock>("statusText").Text);
            Assert.Equal(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(Get<ProgressBar>("fileCountBar").Foreground).Color);
            Assert.Equal(100, Get<ProgressBar>("fileCountBar").Value);
            Assert.Equal(0, server.SyncCalls);

            server.Limits = server.Limits with { MaxNumberOfFiles = 2, MaxBucketSize = 5 };
            Click("btnCompare");
            await Until(() => !Get<Button>("btnCancel").IsVisible);
            Assert.Contains("총 용량이 버킷 제한을 초과", Get<TextBlock>("statusText").Text);
            Assert.Equal(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(Get<ProgressBar>("usageBar").Foreground).Color);
            Assert.NotEqual(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(Get<ProgressBar>("fileCountBar").Foreground).Color);
            Assert.Equal(0, server.SyncCalls);

            // Exactly at the limits is allowed. Lock one local file only after staging.
            server.Limits = server.Limits with { MaxBucketSize = 6 };
            server.RequireUpload = true;
            var lockAcquired = 0;
            server.DuringUpload = () =>
            {
                if (Interlocked.Exchange(ref lockAcquired, 1) == 0)
                    fileLock = new FileStream(Path.Combine(root, "one.txt"), FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            };
            Click("btnCompare");
            await Until(() => !Get<Button>("btnCancel").IsVisible);
            Assert.True(Get<Grid>("workspacePanel").IsVisible, Get<TextBlock>("statusText").Text);
            Assert.Equal("서버 업로드 완료.", Get<TextBlock>("statusText").Text);
            Assert.Equal(100, Get<ProgressBar>("fileCountBar").Value);
            Assert.Equal(100, Get<ProgressBar>("usageBar").Value);
            Assert.NotEqual(Colors.Red, Assert.IsAssignableFrom<ISolidColorBrush>(Get<ProgressBar>("usageBar").Foreground).Color);
            Assert.Equal(2, server.Files.Count);
            Assert.Contains("갱신", Get<TextBlock>("listStatus").Text);
        }
        finally { fileLock?.Dispose(); window.Close(); }

        T Get<T>(string name) where T : Control => window.FindControl<T>(name)!;
        void Click(string name) => Get<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    [AvaloniaFact]
    public async Task FileUploadProgressUpdatesAndFailureReturnsToWorkspace()
    {
        using var directory = new TestDirectory();
        using var server = new FakeBucketServer { RequireUpload = true, FailUpload = true,
            UploadHalfwayGate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var http = new HttpClient(server);
        var window = new MainWindow(directory.Path, http, new ConfigManager(Path.Combine(directory.Path, "config.json")), _ => { });
        window.Show();
        try
        {
            await Until(() => Get<Button>("btnNext").IsEnabled);
            Get<TextBox>("txtUsername").Text = "tester";
            Get<TextBox>("txtPassword").Text = "password";
            Get<TextBox>("txtBucketId").Text = "bucket";
            Get<TextBox>("txtHost").Text = "https://api.test/api";
            Click("btnNext");
            await Until(() => Get<Grid>("workspacePanel").IsVisible && !Get<Button>("btnCancel").IsVisible);
            await File.WriteAllTextAsync(Path.Combine(directory.Path, "buckets", "bucket", "upload.txt"), "abcdef");
            Click("btnCompare");
            await Until(() => Get<ListBox>("changesGrid").ItemCount == 1);
            var item = Assert.Single((IEnumerable<UploadFileItem>)Get<ListBox>("changesGrid").ItemsSource!);
            await Until(() => item.Percent == 50);
            Assert.Equal("50%", item.ProgressText);
            await Until(() => Get<ListBox>("changesGrid").GetVisualDescendants().OfType<TextBlock>()
                .Any(x => x.Text == "50%"));
            await Task.Delay(150); // allow the headless render frame to catch up with bindings
            Capture(window, "upload-progress");
            server.UploadHalfwayGate.SetResult();
            await Until(() => !Get<Button>("btnCancel").IsVisible);
            Assert.False(Get<Grid>("previewPanel").IsVisible);
            Assert.True(Get<Grid>("workspacePanel").IsVisible);
            Assert.Contains("작업을 완료하지 못했습니다", Get<TextBlock>("statusText").Text);
            Assert.Equal("중단됨", item.ProgressText);
            Assert.True(Get<Button>("btnCompare").IsEnabled);

            server.FailUpload = false;
            Click("btnCompare");
            await Until(() => !Get<Button>("btnCancel").IsVisible);
            Assert.True(Get<Grid>("workspacePanel").IsVisible);
            Assert.Equal("서버 업로드 완료.", Get<TextBlock>("statusText").Text);
        }
        finally { server.UploadHalfwayGate.TrySetResult(); window.Close(); }
        T Get<T>(string name) where T : Control => window.FindControl<T>(name)!;
        void Click(string name) => Get<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    private static void AssertClickKeepsBackground(Window window, ListBox list)
    {
        Dispatcher.UIThread.RunJobs();
        using var frame = window.CaptureRenderedFrame();
        var item = Assert.IsType<ListBoxItem>(list.ContainerFromIndex(0));
        var presenter = item.GetVisualDescendants().OfType<ContentPresenter>()
            .First(x => x.Name == "PART_ContentPresenter");
        var point = item.TranslatePoint(new Point(20, item.Bounds.Height / 2), window)!.Value;
        AssertTransparent();
        window.MouseMove(point);
        AssertTransparent();
        window.MouseDown(point, MouseButton.Left);
        AssertTransparent();
        window.MouseUp(point, MouseButton.Left);
        Assert.True(item.IsSelected);
        AssertTransparent();
        window.MouseMove(new Point(1, 1));
        AssertTransparent();

        void AssertTransparent()
        {
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
        }
    }

    private static async Task Until(Func<bool> condition, TimeSpan? timeout = null)
    {
        var end = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        while (!condition())
        {
            if (DateTime.UtcNow > end) throw new TimeoutException("UI did not reach expected state.");
            await Task.Delay(30);
        }
    }

    private static void Capture(Window window, string name)
    {
        var output = Environment.GetEnvironmentVariable("FISH_UI_SCREENSHOTS");
        if (string.IsNullOrEmpty(output)) return;
        Directory.CreateDirectory(output);
        Dispatcher.UIThread.RunJobs();
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(output, name + ".png"));
    }
}
