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
        var readLine = new ReadLine
        {
            HistoryHandler = historyHandler,
            AutoCompletionHandler = new AutoCompleteHandler(shell, new[]{ ' ', '/' }),
            HighlightHandler = new HighlightHandler(shell),
            HintHandler = new HintHandler(historyRepository, shell),
            WordSeparators = new[] { ' ', '/' },
        };
        
        readLine.RegisterShortcut(
            new KeyPress(ConsoleModifiers.Control, ConsoleKey.D),
            _ => Environment.Exit(0)
        );

        /*var random = new Random();
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        for (int i = 0; i < 40000; i++)
        {
            var randomString = new string(Enumerable.Repeat(chars, 50)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            historyHandler.Add(new HistoryEntry
            {
                Path = random.Next(1, 4) == 1 ? "/" : shell.WorkingDirectory,
                Content = randomString,
                Time = DateTime.Now,
            });
        }*/

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
            
            if (input == "exit")
                break;

            shell.RunCommand(input);
        }
    }
}