using System;
using System.IO;
using System.Text;
using BetterReadLine;

namespace Elk.Repl;

static class Repl
{
    public static void Run()
    {
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
        };
        
        string configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config/elk"
        );
        if (!Directory.Exists(configPath))
            Directory.CreateDirectory(configPath);

        string historyFile = Path.Combine(configPath, "history.txt");
        var shell = new ShellSession();
        var readLine = new ReadLine
        {
            AutoCompletionHandler = new AutoCompleteHandler(shell, new[]{ ' ', '/' }),
            HighlightHandler = new HighlightHandler(shell),
            HistoryEnabled = true,
            WordSeparators = new[] { ' ', '/' },
        };
        
        if (File.Exists(historyFile))
            readLine.AddHistory(File.ReadAllLines(historyFile));
        
        readLine.RegisterShortcut(
            new KeyPress(ConsoleModifiers.Control, ConsoleKey.D),
            _ => Environment.Exit(0)
        );

        while (true)
        {
            shell.PrintPrompt();
            
            string input = readLine.Read();
            File.AppendAllText(historyFile, $"{input}\n");
            if (input == "exit")
                break;

            shell.RunCommand(input);
        }
    }
}