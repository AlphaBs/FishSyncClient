using CommandLine;
using FishBucket.ApiClient;
using System.Text;

namespace FishSyncClient.Cli;

[Verb("login", HelpText = "Log in and save a token for this server in the user configuration directory.")]
public class LoginCommand : CommandBase
{
    [Value(0, Required = false, MetaName = "username", HelpText = "Account username. Prompts if omitted.")]
    public string? Username { get; set; }

    [Option("password-stdin", HelpText = "Read the password from the first line of standard input.")]
    public bool PasswordStdin { get; set; }

    protected override async ValueTask<int> RunAsync()
    {
        var host = await GetServer();
        var username = Username;
        if (string.IsNullOrWhiteSpace(username))
        {
            if (Console.IsInputRedirected)
            {
                throw new ArgumentException("표준 입력을 사용하는 경우 login <username>으로 계정 아이디를 지정해 주세요.");
            }

            Console.Write("아이디: ");
            username = Console.ReadLine();
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("계정 아이디를 입력해 주세요.");
        }

        string? password;
        if (PasswordStdin)
        {
            password = await Console.In.ReadLineAsync();
        }
        else
        {
            if (Console.IsInputRedirected)
            {
                throw new ArgumentException("표준 입력으로 비밀번호를 전달하려면 --password-stdin을 사용해 주세요.");
            }

            password = ReadPassword();
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new ArgumentException("비밀번호를 입력해 주세요.");
        }

        using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        var store = new CredentialStore();
        var login = new CliLogin(httpClient, store);
        await login.Login(host, username.Trim(), password);
        Console.WriteLine($"로그인 성공. 토큰 저장 위치: {store.GetPath(host)}");
        return 0;
    }

    private static string ReadPassword()
    {
        Console.Write("비밀번호: ");
        var password = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return password.ToString();
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                {
                    password.Length--;
                }
            }
            else if (!char.IsControl(key.KeyChar))
            {
                password.Append(key.KeyChar);
            }
        }
    }
}

public sealed class CliLogin(HttpClient httpClient, CredentialStore store)
{
    public async Task Login(string server, string username, string password)
    {
        var host = CredentialStore.NormalizeServer(server);
        var api = new FishApiClient(host, httpClient);
        var token = await api.Login(username, password);
        await store.Save(host, token);
    }
}
