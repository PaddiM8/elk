#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Elk.Analysis;
using Elk.Scoping;
using Elk.Std.DataTypes;
using Elk.Vm;

#endregion

namespace Elk;

public class ShellSession(RootModuleScope rootModule, VirtualMachineOptions vmOptions)
{
    public RootModuleScope RootModule
        => _virtualMachine.RootModule;

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

    private readonly VirtualMachine _virtualMachine = new(rootModule, vmOptions);

    public void InitInteractive()
    {
        LoadPaths();
        Environment.SetEnvironmentVariable("OLDPWD", WorkingDirectory);

        var initFile = ResourceProvider.ReadFile("init.elk")!;
        RunCommand(initFile);

        if (File.Exists(CommonPaths.InitFile))
        {
            //RunFile(CommonPaths.InitFile, null);

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
        if (!_virtualMachine.RootModule.FunctionExists("elkPrompt"))
            return $"{WorkingDirectoryUnexpanded} >> ";

        var prompt = _virtualMachine.ExecuteFunction("elkPrompt", [], isRoot: false);
        Environment.SetEnvironmentVariable("?", previousExitCode);

        return prompt.ToString() ?? " >> ";
    }

    public void RunCommand(
        string command,
        bool ownScope = false,
        bool printReturnedValue = true,
        bool printErrorLineNumbers = true)
    {
        var textWriter = Console.Out;
        var resultBuilder = new StringBuilder();
        var evaluationResult = ElkProgram.Evaluate(
            command,
            ownScope
                ? new LocalScope(_virtualMachine.RootModule)
                : _virtualMachine.RootModule,
            AnalysisScope.AppendToModule,
            _virtualMachine
        );

        if (evaluationResult.Diagnostics.Any())
        {
            foreach (var diagnostic in evaluationResult.Diagnostics)
            {
                textWriter = Console.Error;
                resultBuilder.AppendLine(
                    diagnostic.ToString(printErrorLineNumbers).Trim()
                );
            }
        }
        else if (evaluationResult.Value is RuntimeNil or null)
        {
            return;
        }
        else
        {
            resultBuilder.AppendLine(
                evaluationResult.Value.ToString() ?? ""
            );
        }

        if (!printReturnedValue && !evaluationResult.Diagnostics.Any())
        {
            Console.ResetColor();

            return;
        }

        var result = resultBuilder.ToString();
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

    public void RunFile(string filePath, IEnumerable<string>? arguments)
    {
        arguments ??= new List<string>();

        var argumentValues = arguments.Prepend(filePath)
            .Select<string, RuntimeObject>(literal => new RuntimeString(literal));
        var argv = new RuntimeList(argumentValues.ToList());
        _virtualMachine.ShellEnvironment.Argv = argv;

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

        void CallOnExit()
        {
            if (_virtualMachine.RootModule.FunctionExists("__onExit"))
                _virtualMachine.ExecuteFunction("__onExit", [], isRoot: true);
        }

        Console.CancelKeyPress += (_, _) => CallOnExit();
        var evaluationResult = ElkProgram.Evaluate(
            File.ReadAllText(filePath),
            _virtualMachine.RootModule,
            AnalysisScope.OncePerModule,
            _virtualMachine
        );

        CallOnExit();

        if (evaluationResult.Diagnostics.Any())
        {
            foreach (var diagnostic in evaluationResult.Diagnostics)
                Console.Error.WriteLine(diagnostic.ToString().Trim());

            Environment.Exit(1);
        }
    }
}