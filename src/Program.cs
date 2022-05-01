using System;
using CommandLine;
using Elk;

CommandLine.Parser.Default.ParseArguments<CliOptions>(args)
    .WithParsed(options =>
    {
        if (options.FilePath == null)
        {
            Repl.Run();
        }
        else
        {
            new ShellSession().RunFile(options.FilePath);
        }
    });