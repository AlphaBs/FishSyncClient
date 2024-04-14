using CommandLine;
using FishSyncClient.Cli;

Parser.Default.ParseArguments<
    PullCommand,
    PushCommand>(args).MapResult(
        (PullCommand c) => c.Run(),
        (PushCommand c) => c.Run(),
        errors => 
        {
            foreach (var err in errors)
            {
                Console.WriteLine(err);
            }
            return 1;
        }
    );