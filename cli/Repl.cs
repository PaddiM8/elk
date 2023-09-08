#region

using System;
using System.IO;
using System.Linq;
using BetterReadLine;
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
        var readLine = new ReadLine
        {
            HistoryHandler = historyHandler,
            AutoCompletionHandler = new AutoCompleteHandler(shell, new[]{ ' ', '/' }, highlightHandler),
            HighlightHandler = highlightHandler,
            HintHandler = new HintHandler(historyRepository, shell),
            EnterHandler = new EnterHandler(),
            WordSeparators = new[] { ' ', '/' },
        };
        
        readLine.RegisterShortcut(
            new KeyPress(ConsoleModifiers.Control, ConsoleKey.D),
            _ => Environment.Exit(0)
        );

        while (true)
        {
            shell.PrintPrompt();
            
            string input = readLine.Read();
            if (input.Trim().Any())
            {
                historyHandler.Add(new HistoryEntry
                {
                    Path = shell.WorkingDirectory,
                    Content = input,
                    Time = DateTime.Now,
                });
            }
            
            shell.RunCommand(input);
        }
    }
}
