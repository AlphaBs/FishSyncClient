using System.IO;
using System.Text.Json;

namespace FishSyncClient.Gui;

public class Config
{
    public string? Root { get; set; } 
    public string? Host { get; set; }
}

public class ConfigManager
{
    private readonly string _path;

    public ConfigManager(string path) => 
        _path = path;

    private Config? _config;
    public Config Config => _config ?? throw new InvalidOperationException("Load config first");

    public async Task<Config> LoadConfig()
    {
        try
        {
            using var fs = File.OpenRead(_path);
            _config = await JsonSerializer.DeserializeAsync<Config>(fs)
                ?? throw new FormatException("null config");
            Logger.Instance.LogInformation("설정 불러오기 성공");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError("설정 불러오기 실패: " + ex.ToString());
            Logger.Instance.LogInformation("설정 파일 초기화");
            _config = new Config();
        }

        return _config;
    }

    public async Task SaveConfig()
    {
        try
        {
            using var fs = File.Create(_path);
            await JsonSerializer.SerializeAsync(fs, _config);
            Logger.Instance.LogInformation("설정 저장 성공");
        }
        catch (Exception ex)
        {
            Logger.Instance.LogError("설정 저장 실패: " + ex.ToString());
        }
    }
}