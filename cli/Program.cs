#region

using System;
using Elk;
using Elk.Cli;
using Elk.Std.DataTypes.Serialization.CommandLine;

#endregion

Environment.SetEnvironmentVariable("LD_LIBRARY_PATH", "../lib/elk");

var cliParser = new RuntimeCliParser("elk")
    .IgnoreFlagsAfterArguments()
    .AddArgument(new CliArgument
    {
        Identifier = "file_path",
        Description = "Path to the elk file that should be executed.",
    })
    .AddArgument(new CliArgument
    {
        Identifier = "arguments",
        Description = "Arguments for the script.",
        IsVariadic = true,
    })
    .SetAction(result =>
    {
        var filePath = result.GetString("file_path");
        if (filePath == null)
        {
            Repl.Run();
            return;
        }

        ShellSession.RunFile(
            filePath,
            result.GetList("arguments")
        );
    });

cliParser.Parse(args);
