using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

namespace FishSyncClient.Cli;

public static class CliHelp
{
    public const string RootDescription =
        "Sync a local directory with a FishSyncServer bucket. Changes apply immediately; there is no dry-run.";

    public const string PullDescription =
        "Mirror the server bucket into the local directory (server -> local). Deletes local-only files.";

    public const string PushDescription =
        "Mirror the local directory into the server bucket (local -> server). Deletes server-only files.";

    public static string GetUsage(string? command) => command switch
    {
        "pull" or "push" => $"Usage: cli {command} [bucket-id] [options]",
        "files" => "Usage: cli files [bucket-id] [options]",
        "update" => "Usage: cli update",
        _ => "Usage: cli <pull|push> [bucket-id] [options]\n       cli files [bucket-id] [options]\n       cli update"
    };

    public static void Configure(RootCommand root)
    {
        var help = root.Options
            .OfType<HelpOption>()
            .Single();
        if (help.Action is HelpAction action)
        {
            help.Action = new DetailedHelpAction(action);
        }
    }

    // 기본 도움말(설명·사용법·옵션) 뒤에 예시와 설정 요약을 한 번만 덧붙인다.
    private sealed class DetailedHelpAction(HelpAction inner) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            var result = inner.Invoke(parseResult);
            parseResult.InvocationConfiguration.Output.Write(
                GetDetails(parseResult.CommandResult.Command.Name));
            return result;
        }
    }

    private static string GetDetails(string command)
    {
        string[] sections = command switch
        {
            "pull" => [
                """
                Example:
                  cli pull my-bucket --root /absolute/path/files
                Changed local files are overwritten. The server bucket is not modified.
                """,
                Settings
            ],
            "push" => [
                """
                Example:
                  cli push my-bucket --root /absolute/path/files
                Changed server files are overwritten. Pushing an empty directory deletes every server file.
                HTTP 401/403: check FISH_USERNAME, FISH_PASSWORD, the server URL, and write permission for the bucket.
                """,
                Settings
            ],
            "update" => [],
            "files" => [
                """
                Example:
                  cli files my-bucket
                Lists file paths, sizes in bytes, and MD5 checksums.
                Set FISH_USERNAME and FISH_PASSWORD as environment variables or in ./.env.
                Bucket defaults to FISH_BUCKET_ID, then config BucketId.
                Exit codes: 0 = success, 1 = invalid arguments or failure.
                """
            ],
            _ => [
                """
                Quick start:
                  1. Set FISH_USERNAME and FISH_PASSWORD as environment variables or in ./.env
                  2. cli pull my-bucket --root /absolute/path/files
                  3. Edit files, then: cli push my-bucket --root /absolute/path/files

                Run 'cli <command> --help' for settings and exit codes.
                """
            ]
        };
        return string.Join(Environment.NewLine + Environment.NewLine, sections) + Environment.NewLine + Environment.NewLine;
    }

    private const string Settings = """
        Settings:
          Credentials  FISH_USERNAME and FISH_PASSWORD are required, from environment variables or ./.env.
                       A token is obtained on every run and kept in memory only.
          Priority     command-line > environment variable > ./.env > config file > default.
                       Variables: FISH_BUCKET_ID, FISH_ROOT, FISH_SERVER. Config keys: BucketId, Root, Host.
          Warnings     Paths matching config WarningPatterns are listed and need [Y/n] (n or EOF cancels).
                       They are not exclusions: every file under the root is synced.

        Exit codes: 0 = success, 1 = invalid arguments, failure, or cancellation.
          A failed sync may have applied some changes; inspect the result before retrying.
        """;
}
