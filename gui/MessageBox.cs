using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace FishSyncClient.Gui;

public enum MessageBoxButtons
{
    Ok,
    YesNo,
}

public enum MessageBoxResult
{
    Ok,
    Yes,
    No,
}

/// <summary>
/// A small, dependency-free replacement for WPF's MessageBox built on top of an Avalonia window.
/// </summary>
public static class MessageBox
{
    public static Task<MessageBoxResult> Show(string message) =>
        Show(message, "알림", MessageBoxButtons.Ok);

    public static Task<MessageBoxResult> Show(string message, string title) =>
        Show(message, title, MessageBoxButtons.Ok);

    public static Task<MessageBoxResult> Show(string message, string title, MessageBoxButtons buttons)
    {
        if (!Dispatcher.UIThread.CheckAccess())
            return Dispatcher.UIThread.InvokeAsync(() => Show(message, title, buttons));

        var tcs = new TaskCompletionSource<MessageBoxResult>();

        var messageBlock = new SelectableTextBlock
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 16),
        };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        var window = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            MinWidth = 320,
            MaxWidth = 640,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    messageBlock,
                    buttonPanel,
                },
            },
        };

        void Close(MessageBoxResult result)
        {
            tcs.TrySetResult(result);
            window.Close();
        }

        if (buttons == MessageBoxButtons.YesNo)
        {
            var yes = new Button { Content = "예", MinWidth = 80, IsDefault = true };
            var no = new Button { Content = "아니오", MinWidth = 80, IsCancel = true };
            yes.Click += (_, _) => Close(MessageBoxResult.Yes);
            no.Click += (_, _) => Close(MessageBoxResult.No);
            buttonPanel.Children.Add(yes);
            buttonPanel.Children.Add(no);
        }
        else
        {
            var ok = new Button { Content = "확인", MinWidth = 80, IsDefault = true, IsCancel = true };
            ok.Click += (_, _) => Close(MessageBoxResult.Ok);
            buttonPanel.Children.Add(ok);
        }

        // Ensure a result is produced even if the user closes the window with the title bar button.
        window.Closed += (_, _) =>
            tcs.TrySetResult(buttons == MessageBoxButtons.YesNo ? MessageBoxResult.No : MessageBoxResult.Ok);

        var owner = GetOwnerWindow();
        if (owner != null && owner != window)
            window.ShowDialog(owner);
        else
            window.Show();

        return tcs.Task;
    }

    private static Window? GetOwnerWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        return desktop.Windows.FirstOrDefault(w => w.IsActive)
            ?? desktop.MainWindow;
    }
}
