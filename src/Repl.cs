using System;
using BetterReadLine;

namespace Elk;

static class Repl
{
    public static void Run()
    {
        var shell = new ShellSession();
        while (true)
        {
            shell.PrintPrompt();
            
            string input = ReadLine.Read();
            if (input == "exit")
                break;

            shell.RunCommand(input);
        }
    }
}