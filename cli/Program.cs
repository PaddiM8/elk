#region

using Elk;
using Elk.Cli;
using Elk.Std.DataTypes.Serialization.CommandLine;

#endregion

var cliParser = new CommandLineParser<object?>("elk")
    .AddArgument(new CommandLineArgument
    {
        Identifier = "file_path",
        Description = "Path to the elk file that should be executed.",
    })
    .AddArgument(new CommandLineArgument
    {
        Identifier = "arguments",
        Description = "Arguments for the script.",
        IsVariadic = true,
    })
    .SetAction(options =>
    {
        var filePath = options.GetString("file_path");
        if (filePath == null)
        {
            Repl.Run();
        }
        else
        {
            ShellSession.RunFile(
                filePath,
                options.GetList("arguments")
            );
        }

        return null;
    });

cliParser.Run(args, out _);