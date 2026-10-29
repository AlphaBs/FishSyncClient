using System.CommandLine;

namespace FishSyncClient.Cli;

public static class CliApplication
{
    public static int Run(
        string[] args,
        TextWriter output,
        TextWriter errorOutput,
        TextReader? input = null)
    {
        if (args.Length == 0 || args is ["help"])
        {
            args = ["--help"];
        }
        else if (args is ["help", "pull" or "push"])
        {
            args = [args[1], "--help"];
        }

        var root = CreateRootCommand(output, errorOutput, input);
        var configuration = new InvocationConfiguration
        {
            Output = output,
            Error = errorOutput,
            EnableDefaultExceptionHandler = false
        };
        var result = root.Parse(args);
        if (result.Errors.Count > 0 || result.UnmatchedTokens.Count > 0)
        {
            // 인자 오류가 있으면 작업을 실행하지 않고 도움말까지 stderr로 보낸다.
            foreach (var error in result.Errors)
            {
                errorOutput.WriteLine(error.Message);
            }
            var commandName = result.CommandResult.Command.Name;
            string[] helpArguments = commandName is "pull" or "push"
                ? [commandName, "--help"]
                : ["--help"];
            root.Parse(helpArguments)
                .Invoke(new InvocationConfiguration
                {
                    Output = errorOutput,
                    Error = errorOutput,
                    EnableDefaultExceptionHandler = false
                });
            return 1;
        }
        return result.Invoke(configuration);
    }

    public static RootCommand CreateRootCommand(
        TextWriter output,
        TextWriter errorOutput,
        TextReader? input = null)
    {
        var root = new RootCommand(CliHelp.Describe(null))
        {
            TreatUnmatchedTokensAsErrors = true
        };
        root.Subcommands.Add(CreateSyncCommand("pull", () => new PullCommand(), output, errorOutput, input));
        root.Subcommands.Add(CreateSyncCommand("push", () => new PushCommand(), output, errorOutput, input));
        return root;
    }

    private static Command CreateSyncCommand(
        string name,
        Func<SyncCommandBase> createOperation,
        TextWriter output,
        TextWriter errorOutput,
        TextReader? input)
    {
        var bucket = new Argument<string?>("bucket-id")
        {
            Description = "Bucket id. Defaults to FISH_BUCKET_ID or config BucketId.",
            Arity = ArgumentArity.ZeroOrOne
        };
        bucket.Validators.Add(result =>
        {
            // 선택적 위치 인자가 알 수 없는 옵션을 버킷 ID로 받아들이지 않도록 한다.
            var value = result.GetValueOrDefault<string?>();
            if (value?.StartsWith('-') == true)
            {
                result.AddError("버킷 ID 자리에 알 수 없는 옵션이 입력되었습니다. --help로 옵션을 확인해 주세요.");
            }
        });
        var server = new Option<string?>("--server")
        {
            Description = "Server API URL. Overrides environment, .env, config, and the built-in default."
        };
        var config = new Option<string>("--config")
        {
            Description = "JSON settings path relative to the current working directory. Missing file is allowed.",
            DefaultValueFactory = _ => "config/config.json"
        };
        var localRoot = new Option<string?>("--root")
        {
            Description = "Absolute local directory. Overrides FISH_ROOT, config Root, and current working directory."
        };
        var yes = new Option<bool>("--yes", "-y")
        {
            Description = "Log WarningPatterns matches and continue without asking [Y/n]."
        };

        var command = new Command(name, CliHelp.Describe(name))
        {
            TreatUnmatchedTokensAsErrors = true
        };
        command.Arguments.Add(bucket);
        command.Options.Add(server);
        command.Options.Add(config);
        command.Options.Add(localRoot);
        command.Options.Add(yes);
        command.SetAction(result =>
        {
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
