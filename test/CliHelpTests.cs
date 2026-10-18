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
        Assert.Contains("FishSyncClient CLI synchronizes", result.Output);
        Assert.Contains("cli login my-account", result.Output);
        Assert.Contains("cli pull my-bucket", result.Output);
        Assert.Contains("cli push my-bucket", result.Output);
        Assert.Contains("overwrite and delete", result.Output);
        Assert.Contains("CURRENT WORKING DIRECTORY", result.Output);
        Assert.Contains("--server > config Host > FISH_SERVER", result.Output);
        Assert.Contains("--token > FISH_TOKEN > saved login", result.Output);
        Assert.Contains("0 = successful", result.Output);
        Assert.Contains("1 = invalid arguments or failed operation", result.Output);
    }

    [Theory]
    [InlineData("login", "LOGIN:")]
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
        Assert.Contains("credentials/", direct.Output);
    }

    [Fact]
    public void LoginHelpShowsOnlyLoginOptions()
    {
        var result = Run("login", "--help");

        Assert.DoesNotContain("--root", result.Output);
        Assert.DoesNotContain("Token override.", result.Output);
        Assert.Contains("--password-stdin", result.Output);
        Assert.Contains("password input is hidden", result.Output);
        Assert.Contains("The password is never saved", result.Output);
        Assert.Contains("Failed login preserves", result.Output);
    }

    [Fact]
    public void PushHelpExplainsDeletionAndReloginRecovery()
    {
        var result = Run("push", "--help");

        Assert.Contains("deletes server-only files", result.Output);
        Assert.Contains("No dry-run or confirmation", result.Output);
        Assert.Contains("HTTP 401/403", result.Output);
        Assert.Contains("cli login <username> --server", result.Output);
        Assert.Contains("write permission", result.Output);
    }

    [Fact]
    public void NoArgumentsShowsHelpWithoutStartingAnOperation()
    {
        var result = Run();

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains("QUICK START", result.Output);
    }

    [Theory]
    [InlineData("login", "--root", "/tmp/files")]
    [InlineData("login", "--token", "unused")]
    [InlineData("push", "--unknown")]
    [InlineData("help", "unknown-command")]
    public void InvalidArgumentsWriteHelpToStderrAndFail(params string[] args)
    {
        var result = Run(args);

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Output);
        Assert.NotEmpty(result.Error);
        Assert.Contains("Usage:", result.Error);
    }

    [Fact]
    public void VersionDoesNotPrintHelpOrFail()
    {
        var result = Run("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.NotEmpty(result.Output);
        Assert.Empty(result.Error);
        Assert.DoesNotContain("QUICK START", result.Output);
    }

    private static (int ExitCode, string Output, string Error) Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = CliApplication.Run(args, output, error);
        return (exitCode, output.ToString(), error.ToString());
    }
}
