namespace FishSyncClient.Server.Alphabet;

public class LauncherInfo
{
    public string? Name { get; set; }
    public string? GameServerIp { get; set; }
    public string? StartVersion { get; set; }
    public string? StartVanillaVersion { get; set; }
    public string? LauncherServer { get; set; }
    public string[] WhitelistFiles { get; set; } = [];
    public string[] WhitelistDirs { get; set; } = [];
    public string[] IncludeFiles { get; set; } = [];
}