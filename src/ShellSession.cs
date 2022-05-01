using System;
using System.IO;
using Elk.Interpreting;
using Elk.Lexing;
using Elk.Parsing;

namespace Elk;

public class ShellSession
{
    public string WorkingDirectory
    {
        get
        {
            string workingDirectory = _interpreter.ShellEnvironment.WorkingDirectory;
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            return workingDirectory.StartsWith(homePath)
                ? "~" + workingDirectory[homePath.Length..]
                : workingDirectory;
        }
    }

    private readonly Interpreter _interpreter = new();
    
    public ShellSession()
    {
        Init();
    }

    private void Init()
    {
        string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string initFilePath = Path.Combine(homePath, ".config/elk/init.elk");
        if (File.Exists(initFilePath))
        {
            RunFile(initFilePath);
        }
        else
        {
            RunCommand(Resources.Defaults.init_file);
        }
    }

    public void PrintPrompt()
    {
        if (_interpreter.FunctionExists("elkPrompt"))
        {
            var call = new CallExpr(
                new Token(TokenKind.Identifier, "elkPrompt", TextPos.Default),
                new(),
                CallStyle.Parenthesized
            )
            {
                IsRoot = true,
            };
            _interpreter.Interpret(new() { call });

            return;
        }

        // The 'elkPrompt' function should have been created
        // automatically. This is simply a fallback in case
        // something goes wrong.
        Console.ForegroundColor = ConsoleColor.Green;
        Console.Write(WorkingDirectory);
        Console.ResetColor();
        Console.Write(" >> ");
    }

    public void RunCommand(string command)
    {
        var result = _interpreter.Interpret(command, null);
        if (result is RuntimeNil)
            return;

        string resultString = result.ToString() ?? "";
        if (resultString == "" || resultString.EndsWith('\n'))
        {
            Console.Write(resultString);
        }
        else
        {
            Console.WriteLine(resultString);
        }
    }

    public void RunFile(string filePath)
    {
        _interpreter.Interpret(File.ReadAllText(filePath), filePath);
    }
}