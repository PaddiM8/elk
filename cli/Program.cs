#region

using System;
using System.IO;
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
    .AddFlag(new CliFlag
    {
        Identifier = "command",
        ShortName = "c",
        Description = "A command to execute",
        ValueKind = CliValueKind.Text
    })
    .AddFlag(new CliFlag
    {
        Identifier = "highlight",
        LongName = "highlight",
        Description = "Print the file contents with semantic highlighting (ANSI escaped).",
        ValueKind = CliValueKind.Path,
    })
    .SetAction(result =>
    {
        if (result.Contains("command"))
        {
            new ShellSession().RunCommand(result.GetRequiredString("command"));

            return;
        }

        if (result.Contains("highlight"))
        {
            var highlightFile = result.GetRequiredString("highlight");
            if (!File.Exists(highlightFile))
            {
                Console.Error.WriteLine("No such file.");
                return;
            }

            var content = File.ReadAllText(highlightFile);
            var highlighted = new HighlightHandler(new ShellSession()).Highlight(content, 0);
            Console.WriteLine(highlighted);

            return;
        }

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
