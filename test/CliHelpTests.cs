using FishSyncClient.Cli;
using Xunit;

namespace FishSyncClientTest;

public sealed class CliHelpTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("help")]
    public void RootHelpExplainsWorkflowSettingsAndSideEffects(string argument)
    {
        var result = Run(argument);

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("FishSyncServer bucket", result.Output);
        Assert.DoesNotContain("cli login", result.Output);
        Assert.Contains("cli pull my-bucket", result.Output);
        Assert.Contains("cli push my-bucket", result.Output);
        Assert.Contains("Deletes local-only files", result.Output);
        Assert.Contains("Deletes server-only files", result.Output);
        Assert.Contains("no dry-run", result.Output);
        Assert.Contains("FISH_USERNAME and FISH_PASSWORD", result.Output);
        // 명령 설명과 설정 요약은 최상위 도움말에서 반복하지 않는다.
        Assert.DoesNotContain("Settings:", result.Output);
        Assert.Single(result.Output.Split("Deletes local-only files")[1..]);
    }

    [Theory]
    [InlineData("pull", "server -> local")]
    [InlineData("push", "local -> server")]
    public void BothCommandHelpFormsDescribeTheSelectedCommand(string command, string expected)
    {
        var direct = Run(command, "--help");
        var alias = Run("help", command);

        Assert.Equal(0, direct.ExitCode);
        Assert.Equal(0, alias.ExitCode);
        Assert.Empty(direct.Error);
        Assert.Empty(alias.Error);
        Assert.Contains(expected, direct.Output);
        Assert.Contains(expected, alias.Output);
        Assert.Contains("kept in memory only", direct.Output);
        Assert.Contains("command-line > environment variable > ./.env > config file > default", direct.Output);
        Assert.Contains("0 = success", direct.Output);
        Assert.Contains("1 = invalid arguments, failure, or cancellation", direct.Output);
        Assert.Single(direct.Output.Split("Settings:")[1..]);
    }

    [Fact]
    public void LoginCommandIsNoLongerSupported()
    {
        var result = Run("login");
        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.DoesNotContain("cli login", result.Error);
        Assert.Contains("cli <pull|push>", result.Error);
    }

    [Fact]
    public void PushHelpExplainsDeletionAndEnvironmentAuthentication()
    {
        var result = Run("push", "--help");

        Assert.Contains("Deletes server-only files", result.Output);
        Assert.Contains("WarningPatterns", result.Output);
        Assert.Contains("HTTP 401/403", result.Output);
        Assert.Contains("FISH_USERNAME, FISH_PASSWORD", result.Output);
        Assert.DoesNotContain("--token", result.Output);
        Assert.Contains("write permission", result.Output);
    }

    [Fact]
    public void NoArgumentsShowsHelpWithoutStartingAnOperation()
    {
        var result = Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("Quick start", result.Output);
    }

    [Theory]
    [InlineData("pull", "--token", "unused")]
    [InlineData("push", "--token", "unused")]
    [InlineData("login", "--password-stdin")]
    [InlineData("login", "--remember-password")]
    [InlineData("login", "--root", "/tmp/files")]
    [InlineData("login", "--token", "unused")]
    [InlineData("push", "--unknown")]
    [InlineData("pull", "--unknown")]
    [InlineData("push", "bucket", "--unknown")]
    [InlineData("pull", "bucket", "--root")]
    [InlineData("push", "bucket", "--config")]
    [InlineData("help", "unknown-command")]
    public void InvalidArgumentsWriteUsageToStderrAndFail(params string[] args)
    {
        var result = Run(args);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.Contains("Usage: cli ", result.Error);
        Assert.Contains("--help' for details.", result.Error);
        // 오류가 묻히지 않도록 전체 도움말은 출력하지 않는다.
        Assert.DoesNotContain("Options:", result.Error);
        Assert.DoesNotContain("Required command", result.Error);
    }

    [Theory]
    [InlineData("pull")]
    [InlineData("push")]
    public void RelativeRootFailsBeforeAuthentication(string command)
    {
        var result = Run(command, "bucket", "--root", "relative/dir", "--server", "http://127.0.0.1:9");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Root must be an absolute path", result.Error);
    }

    [Fact]
    public void PushOfMissingRootFailsInsteadOfDeletingServerFiles()
    {
        var root = Path.Combine(Path.GetTempPath(), "fish-missing-" + Guid.NewGuid());
        var result = Run("push", "bucket", "--root", root, "--server", "http://127.0.0.1:9");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Root directory does not exist", result.Error);
    }

    [Fact]
    public void VersionDoesNotPrintHelpOrFail()
    {
        var result = Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Output);
        Assert.Empty(result.Error);
        Assert.DoesNotContain("Quick start", result.Output);
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = CliApplication.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
