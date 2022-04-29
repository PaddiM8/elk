using System.IO;
using CommandLine;
using Elk;
using Elk.Interpreting;

CommandLine.Parser.Default.ParseArguments<CliOptions>(args)
    .WithParsedAsync(async options =>
    {
        if (options.FilePath == null)
        {
            await Repl.Run();
        }
        else
        {
            var content = File.ReadAllText(options.FilePath);
            await new Interpreter().Interpret(content, options.FilePath);
        }
    }).Wait();