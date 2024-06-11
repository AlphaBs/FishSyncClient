using CommandLine;
using FishSyncClient.Cli;

Parser.Default.ParseArguments<
    PullCommand,
    PushCommand,
    AlphabetCommand>(args).MapResult(
        (PullCommand c) => c.Run(),
        (PushCommand c) => c.Run(),
        (AlphabetCommand c) => c.Run(),
        errors => 
        {
            foreach (var err in errors)
            {
                Console.WriteLine(err);
            }
            return 1;
        }
    );