using CommandLine;
using CommandLine.Text;

namespace FishSyncClient.Cli;

public static class CliApplication
{
    public static int Run(string[] args, TextWriter output, TextWriter errorOutput)
    {
        if (args.Length == 0)
        {
            args = ["--help"];
        }

        using var parser = new Parser(settings => settings.HelpWriter = null);
        var result = parser.ParseArguments<LoginCommand, PullCommand, PushCommand>(args);
        return result.MapResult(
            (LoginCommand command) => command.Run(),
            (PullCommand command) => command.Run(),
            (PushCommand command) => command.Run(),
            errors =>
            {
                var parseErrors = errors.ToArray();
                if (parseErrors.Any(error => error is VersionRequestedError))
                {
                    output.WriteLine(HeadingInfo.Default);
                    return 0;
                }

                var rootHelpRequested = args.Length == 1 && args[0] is "--help" or "help";
                var helpRequested = rootHelpRequested || parseErrors.Any(error =>
                    error is HelpRequestedError or HelpVerbRequestedError { Matched: true });
                var commandName = args[0];
                if (commandName == "help")
                {
                    commandName = args.ElementAtOrDefault(1);
                }

                var help = CliHelp.Format(result, commandName);
                if (helpRequested)
                {
                    output.WriteLine(help);
                    return 0;
                }

                errorOutput.WriteLine(help);
                return 1;
            });
    }
}
