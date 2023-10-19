#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
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
            var homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

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

        var initFile = EmbeddedResourceProvider.ReadAllText("init.elk")!;
        RunCommand(initFile);

        if (File.Exists(CommonPaths.InitFile))
        {
            RunFile(_interpreter, CommonPaths.InitFile);
        }
        else if (initFile != null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CommonPaths.InitFile)!);
            File.WriteAllText(CommonPaths.InitFile, initFile.Trim());
        }
    }

    private void LoadPaths()
    {
        if (!File.Exists(CommonPaths.PathFile))
            return;

        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? "";
        var initialColon = pathValue == "" ? "" : ":";
        var pathFileContent = File.ReadAllLines(CommonPaths.PathFile);
        if (pathFileContent.Length == 0)
            return;

        pathValue += initialColon + string.Join(":", pathFileContent);
        Environment.SetEnvironmentVariable("PATH", pathValue);
    }

    public bool ModuleExists(ICollection<string> modulePath)
    {
        if (_interpreter.ModuleExists(modulePath))
            return true;

        return modulePath.Count == 1 && StdBindings.HasModule(modulePath.Single());
    }

    public bool StructExists(string name)
        => _interpreter.StructExists(name) || StdBindings.HasRuntimeType(name);

    public bool FunctionExists(string name, ICollection<string>? modulePath = null)
    {
        if (_interpreter.FunctionExists(name, modulePath))
            return true;

        if (modulePath is { Count: > 1 })
            return false;

        return StdBindings.HasFunction(name, modulePath?.SingleOrDefault()) ||
            _interpreter.CurrentModule.FindImportedStdFunctionModule(name) != null;
    }

    public bool VariableExists(string name)
        => _interpreter.VariableExists(name);

    public bool ProgramExists(string name)
        => FileUtils.ExecutableExists(name, WorkingDirectory);

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

            try
            {
                _interpreter.Interpret(new List<Expr> { call });
            }
            catch (RuntimeException e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Error.WriteLine($"Error evaluating elkPrompt: {e.Position} {e.Message}");
                Console.ResetColor();
            }

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
        var textWriter = Console.Out;
        string result;
        try
        {
            var resultObject = _interpreter.Interpret(command);
            if (resultObject is RuntimeNil)
                return;

            result = resultObject.ToString() ?? "";
        }
        catch (RuntimeException e)
        {
            textWriter = Console.Error;
            Console.ForegroundColor = ConsoleColor.Red;
            result = $"{e.Position ?? _interpreter.Position} {e.Message}";
        }
        catch (InvalidOperationException e)
        {
            // Sort/Order methods (eg. in the standard library) throw an exception when
            // they fail to compare two items. This should simply be a runtime error,
            // since that means the user is trying to compare values that can not be
            // compared with each other.
            textWriter = Console.Error;
            Console.ForegroundColor = ConsoleColor.Red;
            result = $"{_interpreter.Position} {e.Message}";
        }

        if (result == "" || result.EndsWith('\n'))
        {
            textWriter.Write(result);
        }
        else
        {
            textWriter.WriteLine(result);
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
        var argv = new RuntimeList(argumentValues);
        interpreter.AddGlobalVariable("argv", argv);
        interpreter.ShellEnvironment.Argv = argv;

        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"No such file: {filePath}");
            Console.ResetColor();

            return;
        }

        try
        {
            interpreter.Interpret(File.ReadAllText(filePath));
        }
        catch (RuntimeException e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"{e.Position} {e.Message}");
            Console.ResetColor();
        }
        catch (InvalidOperationException e)
        {
            // Sort/Order methods (eg. in the standard library) throw an exception when
            // they fail to compare two items. This should simply be a runtime error,
            // since that means the user is trying to compare values that can not be
            // compared with each other.
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"{interpreter.Position} {e.Message}");
            Console.ResetColor();
        }
    }
}