#region

using System;
using System.IO;
using System.Linq;
using Elk.ReadLine;
using Elk.Cli.Database;

#endregion

namespace Elk.Cli;

class Repl
{
    public static void Run()
    {
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
        };

        if (!Directory.Exists(CommonPaths.ConfigFolder))
            Directory.CreateDirectory(CommonPaths.ConfigFolder);

        var shell = new ShellSession();
        const int maxEntries = 50000;
        var historyRepository = new HistoryRepository(maxEntries);
        var historyHandler = HistoryHandler.Init(maxEntries, historyRepository);
        var highlightHandler = new HighlightHandler(shell);
        var readLine = new ReadLinePrompt
        {
            HistoryHandler = historyHandler,
            AutoCompletionHandler = new AutoCompleteHandler(shell, [' ', '/'], highlightHandler),
            HighlightHandler = highlightHandler,
            HintHandler = new HintHandler(
                historyRepository,
                shell,
                highlightHandler,
                new CustomCompletionProvider(shell)
            ),
            EnterHandler = new EnterHandler(),
            SearchHandler = new SearchHandler(historyRepository),
            WordSeparators = [' ', '/', ':'],
        };

        readLine.RegisterShortcut(
            new KeyPress(ConsoleModifiers.Control, ConsoleKey.D),
            _ => Environment.Exit(0)
        );

        while (true)
        {
            var input = readLine.Read(shell.GetPrompt());
            if (input.Trim().Any())
            {
                historyHandler.Add(new HistoryEntry
                {
                    Path = shell.WorkingDirectory,
                    Content = input,
                    Time = DateTime.Now,
                });
            }

            shell.RunCommand(
                input,
                ownScope: false,
                printReturnedValue: true,
                printErrorLineNumbers: false
            );
        }
    }
}
