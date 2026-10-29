using FishSyncClient.Cli;

return CliApplication.Run(
    args,
    Console.Out,
    Console.Error,
    checkForUpdates: CliUpdateService.CheckForUpdates);
