#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Interpreting;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Resources;
using Elk.Std.Bindings;
using Elk.Std.DataTypes;

#endregion

namespace Elk;

public class ShellSession
{
    public string WorkingDirectory
        => ShellEnvironment.WorkingDirectory;

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

    private readonly Interpreter _interpreter = new(null);
    
    public ShellSession()
    {
        Init();
    }

    private void Init()
    {
        LoadPaths();
        RunCommand(Defaults.init_file);

        if (File.Exists(CommonPaths.InitFile))
            RunFile(_interpreter, CommonPaths.InitFile);
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

    public bool ModuleExists(IEnumerable<string> path)
    {
        if (_interpreter.ModuleExists(path))
            return true;

        var single = path.SingleOrDefault();

        return single != null && StdBindings.HasModule(single);
    }

    public bool StructExists(string name)
        => _interpreter.StructExists(name) || StdBindings.HasRuntimeType(name);

    public bool FunctionExists(string name, IEnumerable<string>? modulePath)
        => _interpreter.FunctionExists(name) || StdBindings.HasFunction(name, modulePath?.SingleOrDefault());

    public bool VariableExists(string name)
        => _interpreter.VariableExists(name);

    public bool ProgramExists(string name)
    {
        string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (name.StartsWith('~'))
            name = name[1..] + homePath;

        if (name.StartsWith('.'))
        {
            string absolutePath = Path.Combine(WorkingDirectory, name);

            return Extensions.FileIsExecutable(absolutePath);
        }

        if (name.StartsWith('/'))
            return Extensions.FileIsExecutable(name);

        return Environment.GetEnvironmentVariable("PATH")?
            .Split(":")
            .Any(x => Directory.Exists(x) && Extensions.FileIsExecutable(Path.Combine(x, name))) is true;
    }

    public void PrintPrompt()
    {
        if (Console.CursorLeft != 0)
            Console.WriteLine();

        if (_interpreter.FunctionExists("elkPrompt"))
        {
            var call = new CallExpr(
                new Token(TokenKind.Identifier, "elkPrompt", TextPos.Default),
                Array.Empty<Token>(),
                Array.Empty<Expr>(),
                CallStyle.Parenthesized,
                Plurality.Singular,
                CallType.Function
            )
            {
                IsRoot = true,
            };
            _interpreter.Interpret(new List<Expr> { call });

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
        var result = _interpreter.Interpret(command);
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

    public static void RunFile(string filePath, IEnumerable<string>? arguments)
    {
        RunFile(new Interpreter(filePath), filePath, arguments);
    }

    private static void RunFile(Interpreter interpreter, string filePath, IEnumerable<string>? arguments = null)
    {
        arguments ??= new List<string>();

        var argumentValues = arguments.Prepend(filePath)
            .Select(literal => new RuntimeString(literal));
        interpreter.AddGlobalVariable("argv", new RuntimeList(argumentValues));

        var result = interpreter.Interpret(File.ReadAllText(filePath));
        if (result is RuntimeError err)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(err.ToString());
            Console.ResetColor();
        }
    }
}