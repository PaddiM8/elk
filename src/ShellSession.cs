using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Interpreting;
using Elk.Lexing;
using Elk.Parsing;

namespace Elk;

public class ShellSession
{
    public string WorkingDirectory => _interpreter.ShellEnvironment.WorkingDirectory;

    public string WorkingDirectoryUnexpanded
    {
        get
        {
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            return WorkingDirectory.StartsWith(homePath)
                ? "~" + WorkingDirectory[homePath.Length..]
                : WorkingDirectory;
        }
    }

    private readonly Interpreter _interpreter = new();
    
    public ShellSession()
    {
        Init();
    }

    private void Init()
    {
        LoadPaths();
        RunCommand(Resources.Defaults.init_file);

        if (File.Exists(CommonPaths.InitFile))
            RunFile(CommonPaths.InitFile);
    }

    private void LoadPaths()
    {
        if (!File.Exists(CommonPaths.PathFile))
            return;

        string pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        string initialColon = pathValue == "" ? "" : ":";
        var pathFileContent = File.ReadAllLines(CommonPaths.PathFile);
        if (pathFileContent.Length == 0)
            return;

        pathValue += initialColon + string.Join(":", pathFileContent);
        Environment.SetEnvironmentVariable("PATH", pathValue);
    }

    public bool VariableExists(string name)
    {
        return _interpreter.VariableExists(name);
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
        Console.Write(WorkingDirectoryUnexpanded);
        Console.ResetColor();
        Console.Write(" >> ");
    }

    public void RunCommand(string command)
    {
        var result = _interpreter.Interpret(command, null);
        if (result is RuntimeNil)
            return;

        string resultString = result.ToString() ?? "";
        var textWriter = Console.Out;
        if (result is RuntimeError)
        {
            textWriter = Console.Error;
            Console.ForegroundColor = ConsoleColor.Red;
        }

        if (resultString == "" || resultString.EndsWith('\n'))
        {
            textWriter.Write(resultString);
        }
        else
        {
            textWriter.WriteLine(resultString);
        }

        Console.ResetColor();
    }

    public void RunFile(string filePath, IEnumerable<string>? arguments = null)
    {
        arguments ??= new List<string>();

        var argumentValues = arguments.Prepend(filePath)
            .Select(literal => new RuntimeString(literal));
        _interpreter.AddGlobalVariable("argv", new RuntimeList(argumentValues));

        _interpreter.Interpret(File.ReadAllText(filePath), filePath);
    }
}