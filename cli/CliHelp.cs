namespace FishSyncClient.Cli;

public static class CliHelp
{
    public static string Describe(string? command) =>
        GetIntroduction(command) + "\n" + Settings + "\n" + ExecutionNotes;

    private static string GetIntroduction(string? command)
    {
        return command switch
        {
            "pull" => """
                PULL: Mirror a server bucket into a local directory (server -> local).
                Usage: cli pull [bucket-id] [--root ABSOLUTE_DIR] [--server URL] [--config FILE] [-y]

                Example (set FISH_USERNAME and FISH_PASSWORD in the environment or .env first):
                  cli pull my-bucket --root /absolute/path/files
                Windows root example: --root "C:\sync\files"
                Authenticates automatically for each command; tokens are kept in memory only.
                WARNING: Applies changes immediately, overwrites changed local files, and deletes local-only files.
                The server bucket is not modified. WarningPatterns matches are logged and require [Y/n] (use -y to continue).

                """,
            "push" => """
                PUSH: Mirror a local directory into a server bucket (local -> server).
                Usage: cli push [bucket-id] [--root ABSOLUTE_DIR] [--server URL] [--config FILE] [-y]

                Example (set FISH_USERNAME and FISH_PASSWORD in the environment or .env first):
                  cli push my-bucket --root /absolute/path/files
                Windows root example: --root "C:\sync\files"
                Authenticates automatically for each command; tokens are kept in memory only.
                WARNING: Applies changes immediately, overwrites changed server files, and deletes server-only files.
                WarningPatterns matches are logged and require [Y/n] (use -y to continue). Check the local directory before push.
                HTTP 401/403: verify FISH_USERNAME, FISH_PASSWORD, the API URL, and the account's bucket write permission.

                """,
            _ => """
                FishSyncClient CLI synchronizes a local directory with a FishSyncServer bucket.
                A bucket ID identifies the server-side collection of files you want to synchronize.

                Usage: cli <pull|push> [arguments] [options]
                Use 'cli <command> --help' or 'cli help <command>' for command-specific details.
                From source, replace 'cli' with 'dotnet run --project cli --no-launch-profile --'.

                QUICK START:
                  Set FISH_USERNAME and FISH_PASSWORD in the environment or current-directory .env.
                  cli pull my-bucket --root /absolute/path/files
                  cli push my-bucket --root /absolute/path/files
                Windows root example: --root "C:\sync\files"
                Each command authenticates automatically. Tokens stay in memory; no token file or keychain is used.
                These are separate operations: pull downloads server state, push uploads local state.
                WARNING: Both sync commands can overwrite and delete files immediately.
                WarningPatterns matches require [Y/n]; -y skips the prompt but keeps the warning log. No dry-run is available.

                COMMANDS:
                """
        };
    }

    private const string Settings = """

        SETTINGS:
          --config defaults to config/config.json relative to the CURRENT WORKING DIRECTORY, not the executable.
          A missing config file uses bundled defaults. Default API URL: https://fish2.snowfrost.kr/api.
          Server priority: --server > FISH_SERVER (environment > .env) > config Host > built-in default.
          Current-directory .env is read with DotNetEnv; parent directories are not searched.
          Existing process environment variables override .env; process variables are not modified.
          Supported variables: FISH_SERVER, FISH_ROOT, FISH_BUCKET_ID, FISH_USERNAME, FISH_PASSWORD.
          FISH_USERNAME and FISH_PASSWORD are required; missing/blank values fail with setup instructions.
          Authentication tokens are never saved, printed, or loaded from config/files/environment/keychain.
          Blank settings values are ignored. Command-line options override environment/.env and config.
          Bucket priority: positional bucket-id > FISH_BUCKET_ID (environment/.env) > config BucketId. One is required.
          Root priority: --root > FISH_ROOT (environment/.env) > config Root > current working directory. Use an absolute path.
          Sync includes all files under the root. WarningPatterns are warnings, not exclusion rules.
          Pull checks remote and local paths (including possible deletions); push checks local paths.
          [Y/n]: Enter or Y continues, n or EOF cancels with exit code 1; invalid answers are asked again.
          -y / --yes bypasses confirmation and still logs all matching paths.
          Example config (use C:/sync/files for Root on Windows):
            {"Host":"https://fish2.snowfrost.kr/api","BucketId":"my-bucket","Root":"/absolute/path/files"}
        """;

    private const string ExecutionNotes = """

        AUTOMATION AND EXIT CODES:
          0 = successful operation, help, or version. 1 = invalid arguments or failed operation.
          Help is written to stdout; errors and push HTTP 401/403 notices are written to stderr.
          Progress and operation summaries are human-readable; there is no JSON output mode.
          Failed sync may have applied some changes. Inspect the result before retrying.
        """;
}
