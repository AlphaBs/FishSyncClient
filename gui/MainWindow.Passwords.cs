using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace FishSyncClient.Gui;

public partial class MainWindow
{
    private readonly IPasswordStore _passwordStore;
    private int _passwordRevision;
    private bool _credentialsReady;
    private string? _autofilledPassword;

    private void InitializePasswordControls()
    {
        rememberPassword.IsEnabled = _passwordStore.IsSupported;
        btnForgetPassword.IsEnabled = _passwordStore.IsSupported;
        if (!_passwordStore.IsSupported)
        {
            passwordNotice.Text = "비밀번호 저장은 Windows x64와 macOS x64·ARM64에서 지원합니다.";
        }
        txtUsername.PropertyChanged += CredentialIdentity_Changed;
        txtHost.PropertyChanged += CredentialIdentity_Changed;
        txtPassword.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty)
            {
                return;
            }
            _passwordRevision++;
            if (txtPassword.Text != _autofilledPassword)
            {
                _autofilledPassword = null;
            }
        };
    }

    private async void CredentialIdentity_Changed(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != TextBox.TextProperty || !_credentialsReady || _busy || _closed)
        {
            return;
        }
        _passwordRevision++;
        if (_autofilledPassword != null && txtPassword.Text == _autofilledPassword)
        {
            txtPassword.Text = "";
        }
        rememberPassword.IsChecked = false;
        await LoadSavedPassword();
    }

    private async Task LoadSavedPassword()
    {
        if (!_passwordStore.IsSupported || !string.IsNullOrEmpty(txtPassword.Text))
        {
            return;
        }
        var revision = ++_passwordRevision;
        var host = txtHost.Text?.Trim() ?? "";
        var username = txtUsername.Text?.Trim() ?? "";
        if (username.Length == 0 || !Uri.TryCreate(host, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            return;
        }
        try
        {
            // 타이핑 중에는 요청을 합치고, 이전 계정의 조회 결과는 적용하지 않는다.
            await Task.Delay(300);
            if (revision != _passwordRevision || _closed || _busy)
            {
                return;
            }
            var password = await _passwordStore.Load(host, username);
            if (revision != _passwordRevision || _closed || _busy)
            {
                return;
            }
            if (password != null)
            {
                txtPassword.Text = password;
                _autofilledPassword = password;
                rememberPassword.IsChecked = true;
            }
            passwordNotice.Text = "";
        }
        catch (Exception)
        {
            if (revision == _passwordRevision && !_closed)
            {
                passwordNotice.Text = "저장된 비밀번호를 불러오지 못했습니다. 직접 입력해 로그인할 수 있습니다.";
            }
        }
    }

    private async void ForgetPassword_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy || !_passwordStore.IsSupported)
        {
            return;
        }
        _passwordRevision++;
        _busy = true;
        SetEnabled();
        try
        {
            await _passwordStore.Delete(txtHost.Text ?? "", txtUsername.Text ?? "");
            txtPassword.Text = "";
            rememberPassword.IsChecked = false;
            passwordNotice.Text = "이 서버와 계정의 저장된 비밀번호를 삭제했습니다.";
        }
        catch (Exception)
        {
            passwordNotice.Text = "저장된 비밀번호를 삭제하지 못했습니다. 서버 주소와 계정, OS 키체인 접근 권한을 확인해 주세요.";
        }
        finally
        {
            _busy = false;
            SetEnabled();
        }
    }

    private async Task PersistPassword(string host, string username, string password, bool remember)
    {
        if (!_passwordStore.IsSupported)
        {
            return;
        }
        try
        {
            if (remember)
            {
                await _passwordStore.Save(host, username, password);
            }
            else
            {
                await _passwordStore.Delete(host, username);
            }
            passwordNotice.Text = "";
        }
        catch (Exception)
        {
            passwordNotice.Text = "로그인은 성공했지만 비밀번호 저장 설정을 반영하지 못했습니다. OS 키체인 접근 권한을 확인해 주세요.";
        }
    }

}
