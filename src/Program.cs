using System.IO;
using CommandLine;
using Shel;
using Shel.Interpreting;

CommandLine.Parser.Default.ParseArguments<CliOptions>(args)
    .WithParsed(options =>
    {
        if (options.FilePath == null)
        {
            Repl.Run();
        }
        else
        {
            var content = File.ReadAllText(options.FilePath);
            new Interpreter().Interpret(content, options.FilePath);
        }
    });