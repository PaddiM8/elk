using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BetterReadLine;

namespace Elk;

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
            HistoryEnabled = true,
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

class AutoCompleteHandler : IAutoCompleteHandler
{
    public char[] Separators { get; set; }

    private readonly ShellSession _shell;

    public AutoCompleteHandler(ShellSession shell, char[] separators)
    {
        Separators = separators;
        _shell = shell;
    }

    public int GetCompletionStart(string text, int cursorPos)
    {
        for (int i = cursorPos - 1; i >= 0; i--)
        {
            bool precedingIsBackslash = i > 0 && text[i - 1] == '\\';
            if (Separators.Contains(text[i]) && !precedingIsBackslash)
                return i + 1;
        }

        return 0;
    }

    public string[] GetSuggestions(string text, int startPos, int endPos)
    {
        string word = text[startPos..endPos];

        return Directory.GetFileSystemEntries(_shell.WorkingDirectory)
            .Select(x => Path.GetFileName(x)!)
            .Where(x => x.StartsWith(word))
            .Select(FormatSuggestion)
            .ToArray();
    }

    private string FormatSuggestion(string suggestion)
    {
        string escaped = new Regex("[{}()|$ ]").Replace(suggestion, m => $"\\{m.Value}");

        return Directory.Exists(Path.Combine(_shell.WorkingDirectory, suggestion))
            ? escaped + "/"
            : escaped;
    }
}