using Avalonia.Controls;
using Avalonia.Interactivity;
using FishBucket.ApiClient;
using gui;
using System.Text.Json.Serialization;

namespace FishSyncClient.Gui;

public partial class LoginWindow : Window
{
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly FishApiClient _apiClient;

    // Parameterless constructor for the Avalonia designer.
    public LoginWindow() : this(new FishApiClient("", HttpUtil.HttpClient))
    {
    }

    public LoginWindow(FishApiClient apiClient)
    {
        _apiClient = apiClient;
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var token = _configManager.Config.Token;
        if (string.IsNullOrEmpty(token))
            return;

        updateTokenInfo(token);
    }

    private async void Button_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            this.IsEnabled = false;

            var token = await _apiClient.Login(tbUsername.Text ?? "", tbPassword.Text ?? "");
            _configManager.Config.Token = token;

            await MessageBox.Show("로그인 성공");
        }
        catch (Exception ex)
        {
            await MessageBox.Show(ex.ToString());
        }
        finally
        {
            this.IsEnabled = true;
            updateTokenInfo(_configManager.Config.Token ?? "");
        }
    }

    private async void Button_Click_1(object? sender, RoutedEventArgs e)
    {
        _apiClient.ApiKey = "";
        _configManager.Config.Token = "";
        await MessageBox.Show("로그아웃 완료");

        updateTokenInfo(_configManager.Config.Token ?? "");
    }

    private async void updateTokenInfo(string token)
    {
        try
        {
            tbTokenExpired.IsVisible = false;
            tbToken.Text = token;

            var payload = JwtDecoder.DecodePayload<FishTokenPayload>(token);
            if (payload == null)
                return;

            tbRoles.Text = string.Join(',', payload.Roles);
            tbUsername.Text = payload.Sub;

            var exp = DateTimeOffset.FromUnixTimeSeconds(payload.Exp);
            tbExp.Text = exp.ToString();
            if (exp < DateTimeOffset.Now)
            {
                tbTokenExpired.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            await MessageBox.Show(ex.ToString());

            tbRoles.Text = "";
            tbUsername.Text = "";
            tbExp.Text = "";
        }
    }

    private class FishTokenPayload
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; set; }

        [JsonPropertyName("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")]
        [JsonConverter(typeof(StringOrArrayToListConverter))]
        public List<string> Roles { get; set; } = [];

        [JsonPropertyName("exp")]
        public long Exp { get; set; }
    }
}
