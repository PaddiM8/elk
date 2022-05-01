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
            string workingDirectory = shell.WorkingDirectory;
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (workingDirectory.StartsWith(homePath))
            {
                workingDirectory = "~" + workingDirectory[homePath.Length..];
            }

            string input = ReadLine.Read(workingDirectory + " >> ");
            if (input == "exit")
                break;

            shell.RunCommand(input);
        }
    }
}