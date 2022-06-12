﻿using CommandLine;
using Elk;
using Elk.Repl;

CommandLine.Parser.Default.ParseArguments<CliOptions>(args)
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