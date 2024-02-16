#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Analysis;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

#endregion

namespace Elk;

public class ShellSession
{
    public ModuleScope CurrentModule
        => _interpreter.CurrentModule;

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
        Environment.SetEnvironmentVariable("OLDPWD", WorkingDirectory);

        var initFile = ResourceProvider.ReadFile("init.elk")!;
        RunCommand(initFile);

        if (File.Exists(CommonPaths.InitFile))
        {
            RunFile(_interpreter, CommonPaths.InitFile);

            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(CommonPaths.InitFile)!);
        File.WriteAllText(CommonPaths.InitFile, initFile.Trim());
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

        // TODO: Remove duplicates
        Environment.SetEnvironmentVariable(
            "PATH",
            string.Join(":", pathFileContent) + initialColon + pathValue
        );
    }

    public bool ProgramExists(string name)
        => FileUtils.ExecutableExists(name, WorkingDirectory);

    public string GetPrompt()
    {
        var previousExitCode = Environment.GetEnvironmentVariable("?");

        // The 'elkPrompt' function should have been created
        // automatically. This is simply a fallback in case
        // something goes wrong.
        if (!_interpreter.CurrentModule.FunctionExists("elkPrompt"))
            return $"{WorkingDirectoryUnexpanded} >> ";

        var prompt = CallFunction(_interpreter, "elkPrompt")?.ToString() ?? " >> ";
        Environment.SetEnvironmentVariable("?", previousExitCode);

        return prompt;
    }

    public void RunCommand(
        string command,
        bool ownScope = false,
        bool printReturnedValue = true,
        bool printErrorLineNumbers = true)
    {
        var textWriter = Console.Out;
        string result;
        try
        {
            var resultObject = ElkProgram.Evaluate(
                command,
                ownScope
                    ? new LocalScope(_interpreter.CurrentModule)
                    : _interpreter.CurrentModule,
                AnalysisScope.AppendToModule,
                _interpreter
            ).Value;
            if (resultObject is RuntimeNil or null)
                return;

            result = resultObject.ToString() ?? "";
        }
        catch (RuntimeException e)
        {
            textWriter = Console.Error;
            e.StartPosition ??= _interpreter.Position;
            result = e.ToString(printErrorLineNumbers).Trim();
            if (result == "")
                return;
        }
        catch (Exception e)
        {
            textWriter = Console.Error;
            Console.ForegroundColor = ConsoleColor.Red;
            var position = printErrorLineNumbers
                ? _interpreter.Position
                : null;

            result = $"{position} {e.Message}".Trim();
            if (result == "")
                return;
        }

        if (!printReturnedValue)
        {
            Console.ResetColor();
            return;
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
        interpreter.ShellEnvironment.Argv = argv;

        if (!File.Exists(filePath))
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.Write("Error");
            Console.ResetColor();
            Console.Error.Write(": ");
            Console.Error.WriteLine($"No such file: {filePath}");
            Console.ResetColor();

            return;
        }

        try
        {
            void CallOnExit()
            {
                if (interpreter.CurrentModule.FunctionExists("__onExit"))
                    CallFunction(interpreter, "__onExit");
            }

            Console.CancelKeyPress += (_, _) => CallOnExit();
            ElkProgram.Evaluate(
                File.ReadAllText(filePath),
                new RootModuleScope(filePath, null),
                AnalysisScope.OncePerModule,
                interpreter
            );

            CallOnExit();
        }
        catch (RuntimeException e)
        {
            Console.Error.WriteLine(e);
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.Write("Error");
            Console.ResetColor();
            Console.Error.Write(": ");
            Console.Error.WriteLine($"{interpreter.Position} {e.Message}");
        }
    }

    private static RuntimeObject? CallFunction(Interpreter interpreter, string identifier)
    {
        var call = new CallExpr(
            new Token(TokenKind.Identifier, identifier, TextPos.Default),
            Array.Empty<Token>(),
            Array.Empty<Expr>(),
            CallStyle.Parenthesized,
            Plurality.Singular,
            CallType.Function,
            interpreter.CurrentModule,
            TextPos.Default
        )
        {
            IsRoot = true,
        };

        try
        {
            return ElkProgram.Evaluate(
                new Ast(new List<Expr> { call }),
                interpreter.CurrentModule,
                AnalysisScope.AppendToModule,
                interpreter
            ).Value;
        }
        catch (RuntimeException e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.Write($"Error evaluating {identifier}: ");
            Console.ResetColor();
            Console.WriteLine(e);

            return null;
        }
    }
}