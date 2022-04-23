using System.IO;
using CommandLine;
using Elk;
using Elk.Interpreting;

CommandLine.Parser.Default.ParseArguments<CliOptions>(args)
    .WithParsed(async options =>
    {
        if (options.FilePath == null)
        {
            await Repl.Run();
        }
        else
        {
            var content = File.ReadAllText(options.FilePath);
            new Interpreter().Interpret(content, options.FilePath);
        }
    });