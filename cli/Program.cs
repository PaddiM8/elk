#region

using System;
using System.IO;
using Elk;
using Elk.Cli;
using Elk.LanguageServer;
using Elk.Scoping;
using Elk.Services;
using Elk.Std.Serialization.CommandLine;
using Elk.Vm;

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
        Description = "A command to execute.",
        ValueKind = CliValueKind.Text
    })
    .AddFlag(new CliFlag
    {
        Identifier = "highlight",
        LongName = "highlight",
        Description = "Print the file contents with semantic highlighting (ANSI escaped).",
        ValueKind = CliValueKind.Path,
    })
    .AddFlag(new CliFlag
    {
        Identifier = "lsp",
        LongName = "lsp",
        Description = "Start the language server.",
    })
    .AddFlag(new CliFlag
    {
        Identifier = "dump",
        LongName = "dump",
        Description = "Dump instructions",
    })
    .SetAction(result =>
    {
        var vmOptions = new VirtualMachineOptions
        {
            DumpInstructions = result.Contains("dump"),
        };

        if (result.Contains("command"))
        {
            var scope = new RootModuleScope(null, null);
            new ShellSession(scope, vmOptions).RunCommand(result.GetRequiredString("command"));

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
            var scope = new RootModuleScope(null, null);
            var shellSession = new ShellSession(scope, vmOptions);
            var highlighted = new HighlightHandler(shellSession).Highlight(content, 0);
            Console.WriteLine(highlighted);

            return;
        }

        if (result.Contains("lsp"))
        {
#pragma warning disable VSTHRD002
            ElkLanguageServer.StartAsync().Wait();
#pragma warning restore VSTHRD002

            return;
        }

        var filePath = result.GetString("file_path");
        if (filePath == null)
        {
            try
            {
                Repl.Run(vmOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }

            return;
        }

        var session = new ShellSession(
            new RootModuleScope(filePath, null),
            vmOptions
        );
        session.RunFile(
            filePath,
            result.GetList("arguments")
        );
    });

cliParser.Parse(args);
