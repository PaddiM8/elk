using System;
using BetterReadLine;
using Elk.Interpreting;

namespace Elk;

static class Repl
{
    public static void Run()
    {
        Console.CancelKeyPress += (_, args) =>
        {
            args.Cancel = true;
        };
        
        var shell = new ShellSession();
        var readLine = new ReadLine();
        readLine.RegisterShortcut(
            new KeyPress(ConsoleModifiers.Control, ConsoleKey.D),
            _ => Environment.Exit(0)
        );
        
        while (true)
        {
            shell.PrintPrompt();
            
            string input = readLine.Read();
            if (input == "exit")
                break;

            shell.RunCommand(input);
        }
    }
}