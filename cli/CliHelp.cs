using CommandLine;
using CommandLine.Text;

namespace FishSyncClient.Cli;

public static class CliHelp
{
    public static string Format<T>(ParserResult<T> result, string? command)
    {
        var help = HelpText.AutoBuild(result, maxDisplayWidth: 120);
        help.AddPreOptionsLines(GetIntroduction(command).Split('\n'));
        help.AddPostOptionsLines(GetSettings(command).Split('\n'));
        help.AddPostOptionsLines(ExecutionNotes.Split('\n'));
        return help.ToString();
    }

    private static string GetIntroduction(string? command)
    {
        return command switch
        {
            "login" => """
                LOGIN: Authenticate to a FishSyncServer and save a reusable token for that server.
                Usage: cli login [username] [--server URL] [--config FILE] [--password-stdin]

                Interactive example (password input is hidden):
                  cli login my-account --server https://fish2.snowfrost.kr/api
                Automation example (first line of stdin is the password):
                  cli login my-account --server https://fish2.snowfrost.kr/api --password-stdin < /secure/password.txt
                With redirected input, username and --password-stdin are required.
                The password is never saved. Failed login preserves the previous saved token.
                No bucket ID or local directory is needed. Login does not synchronize any files.

                OPTIONS:
                """,
            "pull" => """
                PULL: Mirror a server bucket into a local directory (server -> local).
                Usage: cli pull [bucket-id] [--root ABSOLUTE_DIR] [--server URL] [--config FILE] [--token TOKEN]

                Example:
                  cli pull my-bucket --root /absolute/path/files --server https://fish2.snowfrost.kr/api
                Windows root example: --root "C:\sync\files"
                Log in to the same server first; a saved login token is reused automatically.
                WARNING: Applies changes immediately, overwrites changed local files, and deletes local-only files.
                The server bucket is not modified. No dry-run or confirmation prompt is available.

                OPTIONS:
                """,
            "push" => """
                PUSH: Mirror a local directory into a server bucket (local -> server).
                Usage: cli push [bucket-id] [--root ABSOLUTE_DIR] [--server URL] [--config FILE] [--token TOKEN]

                Example:
                  cli push my-bucket --root /absolute/path/files --server https://fish2.snowfrost.kr/api
                Windows root example: --root "C:\sync\files"
                Log in to the same server first; a saved login token is reused automatically.
                WARNING: Applies changes immediately, overwrites changed server files, and deletes server-only files.
                No dry-run or confirmation prompt is available. Check the local directory before running push.
                HTTP 401/403: run 'cli login <username> --server <same API URL>', then retry push.
                If 403 persists, check the account's bucket write permission.

                OPTIONS:
                """,
            _ => """
                FishSyncClient CLI synchronizes a local directory with a FishSyncServer bucket.
                A bucket ID identifies the server-side collection of files you want to synchronize.

                Usage: cli <login|pull|push> [arguments] [options]
                Use 'cli <command> --help' or 'cli help <command>' for command-specific details.
                From source, replace 'cli' with 'dotnet run --project cli --no-launch-profile --'.

                QUICK START:
                  cli login my-account --server https://fish2.snowfrost.kr/api
                  cli pull my-bucket --root /absolute/path/files --server https://fish2.snowfrost.kr/api
                  cli push my-bucket --root /absolute/path/files --server https://fish2.snowfrost.kr/api
                Windows root example: --root "C:\sync\files"
                Login prompts for a hidden password and saves a token; pull/push reuse it for the same server.
                These are separate operations: pull downloads server state, push uploads local state.
                WARNING: Both sync commands can overwrite and delete files immediately. There is no dry-run or confirmation.

                COMMANDS:
                """
        };
    }

    private static string GetSettings(string? command)
    {
        var settings = """

            SETTINGS:
              --config defaults to config/config.json relative to the CURRENT WORKING DIRECTORY, not the executable.
              A missing config file is allowed. There is no built-in server URL; specify one via the sources below.
              Server priority: --server > config Host > FISH_SERVER.
              Blank values are ignored. CLI options override config; config Host overrides FISH_SERVER.
            """;

        if (command != "login")
        {
            settings += """

              Bucket priority: positional bucket-id > config BucketId. One is required for pull/push.
              Root priority: --root > config Root > current working directory. Use an absolute path.
              Token priority: --token > FISH_TOKEN > saved login for this server > config Token.
              Sync includes all files under the root; GUI WarningPatterns are not applied by the CLI.
              Example config (use C:/sync/files for Root on Windows):
                {"Host":"https://fish2.snowfrost.kr/api","BucketId":"my-bucket","Root":"/absolute/path/files"}
            """;
        }

        return settings + """


            SAVED LOGIN:
              Tokens are keyed by normalized server API URL (including the API path); other servers do not reuse them.
              Linux: ${XDG_CONFIG_HOME:-~/.config}/fishsyncclient/credentials/<server SHA256>.json
              Windows: %LOCALAPPDATA%/FishSyncClient/credentials/<server SHA256>.json
              macOS: ~/Library/Application Support/FishSyncClient/credentials/<server SHA256>.json
              Tokens are stored as plaintext; Linux/macOS directory permissions are 700 and token files are 600.
              Windows uses the user profile's filesystem permissions. Passwords are not saved.
              Login prints the saved file path, never the token. Delete that file to remove the saved login.
              When a token expires, log in again to the same API URL. --token and FISH_TOKEN override saved login;
              remove a stale override before retrying. A new login does not update these overrides or config Token.
            """;
    }

    private const string ExecutionNotes = """

        AUTOMATION AND EXIT CODES:
          0 = successful operation, help, or version. 1 = invalid arguments or failed operation.
          Help is written to stdout; errors and push HTTP 401/403 relogin notices are written to stderr.
          Progress and operation summaries are human-readable; there is no JSON output mode.
          Failed sync may have applied some changes. Inspect the result before retrying.
        """;
}
