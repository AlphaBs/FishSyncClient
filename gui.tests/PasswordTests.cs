using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using FishSyncClient.Gui;
using Xunit;

namespace FishSyncClient.Gui.Tests;

public class PasswordTests
{
    [Fact]
    public void CredentialKeysNormalizeServerAndSeparateAccountsAndPaths()
    {
        Assert.Equal(PasswordStore.GetKey("https://EXAMPLE.com:443/api/", " user "), PasswordStore.GetKey("https://example.com/api", "user"));
        Assert.NotEqual(PasswordStore.GetKey("https://example.com/api", "user"), PasswordStore.GetKey("https://example.com/api", "User"));
        Assert.NotEqual(PasswordStore.GetKey("https://example.com/api", "user"), PasswordStore.GetKey("https://example.com/other", "user"));
        Assert.NotEqual(PasswordStore.GetKey("http://example.com/api", "user"), PasswordStore.GetKey("https://example.com/api", "user"));
        Assert.Throws<ArgumentException>(() => PasswordStore.GetKey("https://user:secret@example.com", "user"));
    }

    [AvaloniaTheory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task SuccessfulLoginAppliesChoiceWithoutWritingPasswordToConfig(bool remember, bool fail)
    {
        using var directory = new TestDirectory();
        using var http = new HttpClient(new FakeBucketServer());
        var config = new ConfigManager(Path.Combine(directory.Path, "config.json"));
        var store = new FakePasswordStore { FailWrites = fail };
        var window = new MainWindow(directory.Path, http, config, _ => { }, passwordStore: store);
        window.Show();
        try
        {
            await Until(() => Get<Button>(window, "btnNext").IsEnabled);
            Get<TextBox>(window, "txtUsername").Text = "tester";
            Get<TextBox>(window, "txtHost").Text = "https://api.test/api";
            Get<TextBox>(window, "txtPassword").Text = "비밀-password";
            Get<TextBox>(window, "txtBucketId").Text = "bucket";
            await Task.Delay(50);
            Get<CheckBox>(window, "rememberPassword").IsChecked = remember;
            Click(window, "btnNext");
            await Until(() => Get<Grid>(window, "workspacePanel").IsVisible && !Get<Button>(window, "btnCancel").IsVisible);
            Assert.Equal(remember ? 1 : 0, store.SaveCalls);
            Assert.Equal(remember ? 0 : 1, store.DeleteCalls);
            if (remember && !fail)
            {
                Assert.Equal("비밀-password", store.Password);
            }
            var json = await File.ReadAllTextAsync(Path.Combine(directory.Path, "config.json"));
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.Null(config.Config.Token);
            if (fail)
            {
                Assert.Contains("반영하지 못했습니다", Get<TextBlock>(window, "passwordNotice").Text);
            }
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task SavedPasswordLoadsAndCanBeDeletedImmediately()
    {
        using var directory = new TestDirectory();
        using var http = new HttpClient(new FakeBucketServer());
        var config = new ConfigManager(Path.Combine(directory.Path, "config.json"));
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "config.json"), """
            {"Username":"tester","Host":"https://api.test/api","BucketId":"bucket"}
            """);
        var store = new FakePasswordStore { Password = "saved-secret" };
        var window = new MainWindow(directory.Path, http, config, _ => { }, passwordStore: store);
        window.Show();
        try
        {
            await Until(() => Get<Button>(window, "btnNext").IsEnabled);
            Assert.Equal("saved-secret", Get<TextBox>(window, "txtPassword").Text);
            Assert.True(Get<CheckBox>(window, "rememberPassword").IsChecked);
            Click(window, "btnForgetPassword");
            await Until(() => store.DeleteCalls == 1);
            Assert.Null(store.Password);
            Assert.Equal("", Get<TextBox>(window, "txtPassword").Text);
            Assert.False(Get<CheckBox>(window, "rememberPassword").IsChecked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task PendingLoadDoesNotOverwriteManualPasswordOrAnotherAccount()
    {
        using var directory = new TestDirectory();
        using var http = new HttpClient(new FakeBucketServer());
        var config = new ConfigManager(Path.Combine(directory.Path, "config.json"));
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FakePasswordStore { PendingLoad = pending.Task };
        var window = new MainWindow(directory.Path, http, config, _ => { }, passwordStore: store);
        window.Show();
        try
        {
            await Until(() => Get<Button>(window, "btnNext").IsEnabled);
            Get<TextBox>(window, "txtUsername").Text = "first";
            await Until(() => store.LoadCalls > 0);
            Get<TextBox>(window, "txtUsername").Text = "second";
            Get<TextBox>(window, "txtPassword").Text = "manual-secret";
            await Task.Delay(50);
            pending.SetResult("first-account-secret");
            await Task.Delay(400);
            Assert.Equal("manual-secret", Get<TextBox>(window, "txtPassword").Text);
            Assert.False(Get<CheckBox>(window, "rememberPassword").IsChecked);
        }
        finally
        {
            pending.TrySetResult(null);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task ChangingServerClearsAutofilledPasswordBeforeNextLookup()
    {
        using var directory = new TestDirectory();
        using var http = new HttpClient(new FakeBucketServer());
        var config = new ConfigManager(Path.Combine(directory.Path, "config.json"));
        var store = new FakePasswordStore { Password = "saved-secret" };
        var window = new MainWindow(directory.Path, http, config, _ => { }, passwordStore: store);
        window.Show();
        try
        {
            await Until(() => Get<Button>(window, "btnNext").IsEnabled);
            Get<TextBox>(window, "txtUsername").Text = "tester";
            await Until(() => Get<TextBox>(window, "txtPassword").Text == "saved-secret");
            store.Password = null;
            Get<TextBox>(window, "txtHost").Text = "https://other.test/api";
            Assert.Equal("", Get<TextBox>(window, "txtPassword").Text);
            Assert.False(Get<CheckBox>(window, "rememberPassword").IsChecked);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task RejectedLoginDoesNotSaveOrDeleteExistingPassword()
    {
        using var directory = new TestDirectory();
        using var http = new HttpClient(new UnauthorizedServer());
        var config = new ConfigManager(Path.Combine(directory.Path, "config.json"));
        var store = new FakePasswordStore { Password = "old-secret" };
        var window = new MainWindow(directory.Path, http, config, _ => { }, passwordStore: store);
        window.Show();
        try
        {
            await Until(() => Get<Button>(window, "btnNext").IsEnabled);
            Get<TextBox>(window, "txtUsername").Text = "tester";
            Get<TextBox>(window, "txtPassword").Text = "incorrect-secret";
            Get<TextBox>(window, "txtBucketId").Text = "bucket";
            Get<CheckBox>(window, "rememberPassword").IsChecked = true;
            Click(window, "btnNext");
            await Until(() => Get<TextBlock>(window, "statusText").Text!.Contains("작업을 완료하지 못했습니다"));
            Assert.Equal(0, store.SaveCalls);
            Assert.Equal(0, store.DeleteCalls);
            Assert.Equal("old-secret", store.Password);
            Assert.True(Get<Control>(window, "loginPanel").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnavailableKeychainKeepsManualLoginEnabled(bool supported)
    {
        using var directory = new TestDirectory();
        using var http = new HttpClient(new FakeBucketServer());
        var path = Path.Combine(directory.Path, "config.json");
        await File.WriteAllTextAsync(path, """
            {"Username":"tester","Host":"https://api.test/api","BucketId":"bucket"}
            """);
        var store = new FakePasswordStore { IsSupported = supported, FailReads = true };
        var window = new MainWindow(directory.Path, http, new ConfigManager(path), _ => { }, passwordStore: store);
        window.Show();
        try
        {
            await Until(() => Get<Button>(window, "btnNext").IsEnabled);
            Assert.Equal(supported, Get<CheckBox>(window, "rememberPassword").IsEnabled);
            Assert.Equal(supported, Get<Button>(window, "btnForgetPassword").IsEnabled);
            Assert.False(string.IsNullOrWhiteSpace(Get<TextBlock>(window, "passwordNotice").Text));
            Get<TextBox>(window, "txtPassword").Text = "manual-secret";
            Click(window, "btnNext");
            await Until(() => Get<Grid>(window, "workspacePanel").IsVisible && !Get<Button>(window, "btnCancel").IsVisible);
            Assert.Equal(0, store.SaveCalls);
        }
        finally
        {
            window.Close();
        }
    }

    [OsKeychainFact]
    public async Task NativeKeychainRoundTripUpdatesAndDeletesUnicodePassword()
    {
        var store = new PasswordStore();
        var username = "test-" + Guid.NewGuid().ToString("N");
        const string server = "https://keychain-test.invalid/api";
        try
        {
            Assert.Null(await store.Load(server, username));
            await store.Save(server, username, "비밀번호 🔑");
            Assert.Equal("비밀번호 🔑", await store.Load(server + "/", username));
            await store.Save(server, username, "변경된 비밀번호");
            Assert.Equal("변경된 비밀번호", await store.Load(server, username));
            await store.Delete(server, username);
            Assert.Null(await store.Load(server, username));
            await store.Delete(server, username);
        }
        finally
        {
            await store.Delete(server, username);
        }
    }

    private sealed class UnauthorizedServer : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));
        }
    }

    private static T Get<T>(Window window, string name) where T : Control => window.FindControl<T>(name)!;

    private static void Click(Window window, string name) => Get<Button>(window, name)
        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "UI did not reach expected state.");
            await Task.Delay(20);
        }
    }

}

internal sealed class FakePasswordStore : IPasswordStore
{
    public bool IsSupported { get; init; } = true;
    public bool FailReads { get; init; }
    public string? Password { get; set; }
    public Task<string?>? PendingLoad { get; init; }
    public bool FailWrites { get; init; }
    public int LoadCalls { get; private set; }
    public int SaveCalls { get; private set; }
    public int DeleteCalls { get; private set; }

    public Task<string?> Load(string server, string username)
    {
        LoadCalls++;
        if (FailReads)
        {
            throw new IOException("keychain unavailable");
        }
        return PendingLoad ?? Task.FromResult(Password);
    }

    public Task Save(string server, string username, string password)
    {
        SaveCalls++;
        if (FailWrites)
        {
            throw new IOException("keychain unavailable");
        }
        Password = password;
        return Task.CompletedTask;
    }

    public Task Delete(string server, string username)
    {
        DeleteCalls++;
        Password = null;
        return Task.CompletedTask;
    }
}

public sealed class OsKeychainFactAttribute : FactAttribute
{
    public OsKeychainFactAttribute()
    {
        if (!new PasswordStore().IsSupported)
        {
            Skip = "OS 키체인 통합 테스트는 Windows x64와 macOS x64·ARM64에서 실행합니다.";
        }
    }
}
