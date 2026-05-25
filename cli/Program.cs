using CommandLine;
using FishSyncClient.Cli;

Parser.Default.ParseArguments<
    PullCommand,
    PushCommand>(args).MapResult(
        (PullCommand c) => c.Run(),
        (PushCommand c) => c.Run(),
        errors => 
        {
            if (errors.Any(error => error is HelpRequestedError or HelpVerbRequestedError or VersionRequestedError))
                return 0;

            foreach (var err in errors)
            {
                Console.WriteLine(err);
            }
            return 1;
        }
    );
