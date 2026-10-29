namespace FishSyncClient.Cli;

public sealed class CliEnvironment
{
    private readonly Func<string, string?> _readEnvironment;
    private readonly Lazy<Dictionary<string, string>> _values;

    public CliEnvironment(
        string? path = null,
        Func<string, string?>? readEnvironment = null)
    {
        _readEnvironment = readEnvironment ?? Environment.GetEnvironmentVariable;
        _values = new Lazy<Dictionary<string, string>>(
            () => Load(path ?? Path.Combine(Environment.CurrentDirectory, ".env")));
    }

    public string? Get(string name)
    {
        var value = _readEnvironment(name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }
        return _values.Value.GetValueOrDefault(name);
    }

    private static Dictionary<string, string> Load(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path))
        {
            return values;
        }
        try
        {
            // 프로세스 환경변수를 바꾸지 않고 현재 명령에서만 사용한다.
            var entries = DotNetEnv.Env.NoEnvVars()
                .Load(path);
            foreach (var entry in entries)
            {
                values[entry.Key] = entry.Value;
            }
            return values;
        }
        catch (Exception)
        {
            // 파서 오류 메시지에는 비밀번호를 포함한 원문이 들어갈 수 있다.
            throw new FormatException("Could not read .env. Check the file permissions and KEY=value syntax.");
        }
    }
}
