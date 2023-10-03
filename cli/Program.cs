#region

using Elk;
using Elk.Cli;
using Elk.Std.DataTypes.Serialization.CommandLine;

#endregion

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
    });

var options = cliParser.Run(args);
if (options == null)
    return;

var filePath = options.GetString("file_path");
if (filePath == null)
{
    Repl.Run();
    return;
}

ShellSession.RunFile(
    filePath,
    options.GetList("arguments")
);
