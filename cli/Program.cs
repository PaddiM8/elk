#region

using CommandLine;
using Elk;
using Elk.Cli;

#endregion

Parser.Default.ParseArguments<CliOptions>(args)
    .WithParsed(options =>
    {
        if (options.FilePath == null)
        {
            Repl.Run();
        }
        else
        {
            new ShellSession().RunFile(options.FilePath, options.Arguments);
        }
    });
