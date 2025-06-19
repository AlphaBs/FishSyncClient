using FishBucket.ApiClient;
using gui;
using System.Text.Json.Serialization;
using System.Windows;

namespace FishSyncClient.Gui;

public partial class LoginWindow : Window
{
    private readonly ConfigManager _configManager = ConfigManager.Instance;
    private readonly FishApiClient _apiClient;

    public LoginWindow(FishApiClient apiClient)
    {
        _apiClient = apiClient;
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var token = _configManager.Config.Token;
        if (string.IsNullOrEmpty(token))
            return;

        updateTokenInfo(token);
    }

    private async void Button_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            this.IsEnabled = false;

            var token = await _apiClient.Login(tbUsername.Text, tbPassword.Password);
            _configManager.Config.Token = token;

            MessageBox.Show("로그인 성공");
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());
        }
        finally
        {
            this.IsEnabled = true;
            updateTokenInfo(_configManager.Config.Token ?? "");
        }
    }

    private void Button_Click_1(object sender, RoutedEventArgs e)
    {
        _apiClient.ApiKey = "";
        _configManager.Config.Token = "";
        MessageBox.Show("로그아웃 완료");

        updateTokenInfo(_configManager.Config.Token ?? "");
    }

    private void updateTokenInfo(string token)
    {
        try
        {
            tbTokenExpired.Visibility = Visibility.Hidden;
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
                tbTokenExpired.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.ToString());

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
