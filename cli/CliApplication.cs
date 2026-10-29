using System.CommandLine;

namespace FishSyncClient.Cli;

public static class CliApplication
{
    public static int Run(
        string[] args,
        TextWriter output,
        TextWriter errorOutput,
        TextReader? input = null,
        Action<TextWriter>? checkForUpdates = null,
        Func<TextWriter, int>? showLatestUpdate = null)
    {
        if (args.Length == 0 || args is ["help"])
        {
            args = ["--help"];
        }
        else if (args is ["help", "pull" or "push" or "update"])
        {
            args = [args[1], "--help"];
        }

        var root = CreateRootCommand(output, errorOutput, input, checkForUpdates, showLatestUpdate);
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = errorOutput,
            EnableDefaultExceptionHandler = false
        };
        var result = root.Parse(args);
        if (result.Errors.Count > 0 || result.UnmatchedTokens.Count > 0)
        {
            // 인자 오류는 작업을 실행하지 않고, 오류가 묻히지 않도록 전체 도움말 대신 사용법만 출력한다.
            foreach (var error in result.Errors)
            {
                errorOutput.WriteLine(error.Message);
            }
            var commandName = result.CommandResult.Command.Name;
            var helpCommand = commandName is "pull" or "push" or "update"
                ? $"cli {commandName} --help"
                : "cli --help";
            errorOutput.WriteLine(CliHelp.GetUsage(commandName));
            errorOutput.WriteLine($"Run '{helpCommand}' for details.");
            return 1;
        }
        return result.Invoke(configuration);
    }

    public static RootCommand CreateRootCommand(
        TextWriter output,
        TextWriter errorOutput,
        TextReader? input = null,
        Action<TextWriter>? checkForUpdates = null,
        Func<TextWriter, int>? showLatestUpdate = null)
    {
        var root = new RootCommand(CliHelp.RootDescription)
        {
            TreatUnmatchedTokensAsErrors = true
        };
        root.Subcommands.Add(CreateSyncCommand(
            "pull", CliHelp.PullDescription, () => new PullCommand(),
            output, errorOutput, input, checkForUpdates));
        root.Subcommands.Add(CreateSyncCommand(
            "push", CliHelp.PushDescription, () => new PushCommand(),
            output, errorOutput, input, checkForUpdates));
        var update = new Command("update", "Check for updates now and print the latest version's download URL.")
        {
            TreatUnmatchedTokensAsErrors = true
        };
        update.SetAction(_ => (showLatestUpdate ?? CliUpdateService.ShowLatest)(output));
        root.Subcommands.Add(update);
        // 명령 없이 옵션만 받은 경우 "Required command" 대신 사용법을 안내한다.
        root.SetAction(_ =>
        {
            errorOutput.WriteLine(CliHelp.GetUsage(null));
            errorOutput.WriteLine("Run 'cli --help' for details.");
            return 1;
        });
        CliHelp.Configure(root);
        return root;
    }

    private static Command CreateSyncCommand(
        string name,
        string description,
        Func<SyncCommandBase> createOperation,
        TextWriter output,
        TextWriter errorOutput,
        TextReader? input,
        Action<TextWriter>? checkForUpdates)
    {
        var bucket = new Argument<string?>("bucket-id")
        {
            Description = "Bucket ID. Defaults to FISH_BUCKET_ID, then config BucketId.",
            Arity = ArgumentArity.ZeroOrOne
        };
        bucket.Validators.Add(result =>
        {
            // 선택적 위치 인자가 알 수 없는 옵션을 버킷 ID로 받아들이지 않도록 한다.
            var value = result.GetValueOrDefault<string?>();
            if (value?.StartsWith('-') == true)
            {
                result.AddError($"Unrecognized option '{value}'.");
            }
        });
        var server = new Option<string?>("--server")
        {
            Description = $"Server API URL. Defaults to FISH_SERVER, config Host, then {CliConfig.DefaultServer}",
            HelpName = "URL"
        };
        var config = new Option<string>("--config")
        {
            Description = "Config JSON path, relative to the current directory (not the executable). A missing file uses built-in defaults.",
            HelpName = "FILE",
            DefaultValueFactory = _ => "config/config.json"
        };
        var localRoot = new Option<string?>("--root")
        {
            Description = "Absolute local directory. Defaults to FISH_ROOT, config Root, then the current directory.",
            HelpName = "DIR"
        };
        var yes = new Option<bool>("--yes", "-y")
        {
            Description = "Skip the [Y/n] prompt for WarningPatterns matches (they are still listed)."
        };

        var command = new Command(name, description)
        {
            TreatUnmatchedTokensAsErrors = true
        };
        command.Arguments.Add(bucket);
        command.Options.Add(localRoot);
        command.Options.Add(server);
        command.Options.Add(config);
        command.Options.Add(yes);
        command.SetAction(result =>
        {
            checkForUpdates?.Invoke(output);
            // 파싱 결과를 직접 전달하므로 명령 생성에 리플렉션이 필요하지 않다.
            var operation = createOperation();
            operation.BucketId = result.GetValue(bucket);
            operation.Server = result.GetValue(server);
            operation.ConfigPath = result.GetRequiredValue(config);
            operation.Root = result.GetValue(localRoot);
            operation.AssumeYes = result.GetValue(yes);
            return operation.Run(input, output, errorOutput);
        });
        return command;
    }
}
