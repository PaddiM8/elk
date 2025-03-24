#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Elk.Analysis;
using Elk.Exceptions;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Scoping;
using Elk.Std.DataTypes;
using Elk.Vm;

#endregion

namespace Elk;

public class ShellSession
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

    private readonly VirtualMachine _virtualMachine;

    public ShellSession(RootModuleScope rootModule, VirtualMachineOptions vmOptions)
    {
        _virtualMachine = new VirtualMachine(rootModule, vmOptions);

        LoadLocale();
    }

    public void InitInteractive()
    {
        LoadPaths();
        PathCache.RefreshInBackground();
        Environment.SetEnvironmentVariable("OLDPWD", WorkingDirectory);
        Console.OutputEncoding = Encoding.UTF8;

        if (!File.Exists(CommonPaths.InitFile))
            return;

        var previousFilePath = _virtualMachine.RootModule.FilePath;
        _virtualMachine.RootModule.FilePath = CommonPaths.InitFile;
        RunFile(CommonPaths.InitFile, null);
        _virtualMachine.RootModule.FilePath = previousFilePath;
    }

    public RuntimeObject RunFunction(string name, IList<RuntimeObject> arguments)
        => _virtualMachine.ExecuteFunction(name, arguments, isRoot: false);

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

    public bool ProgramExists(string name, bool waitForCache = true)
        => FileUtils.ExecutableExists(name, WorkingDirectory, waitForCache);

    public string GetPrompt()
    {
        PathCache.RefreshInBackground();

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

    private static void LoadLocale()
    {
        if (!File.Exists("/etc/profile.d/locale.sh"))
        {
            return;
        }

        // /etc/profile.d/locale.sh translated to C#
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LANG")))
        {
            var xdgConfigHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var home = Environment.GetEnvironmentVariable("HOME");

            if (!string.IsNullOrEmpty(xdgConfigHome) && File.Exists(Path.Combine(xdgConfigHome, "locale.conf")))
            {
                LoadLocaleConfig(Path.Combine(xdgConfigHome, "locale.conf"));
            }
            else if (!string.IsNullOrEmpty(home) && File.Exists(Path.Combine(home, ".config", "locale.conf")))
            {
                LoadLocaleConfig(Path.Combine(home, ".config", "locale.conf"));
            }
            else if (File.Exists("/etc/locale.conf"))
            {
                LoadLocaleConfig("/etc/locale.conf");
            }
        }

        // Set LANG to default value 'C' if still unset
        var lang = Environment.GetEnvironmentVariable("LANG") ?? "C";
        Environment.SetEnvironmentVariable("LANG", lang);
    }

    private static void LoadLocaleConfig(string filePath)
    {
        foreach (var line in File.ReadAllLines(filePath))
        {
            // Ignore comments and empty lines
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var key = parts[0].Trim();
                var value = parts[1].Trim().Trim('"'); // Remove potential quotes
                Environment.SetEnvironmentVariable(key, value);
            }
        }
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
            var evaluationResultValue = GetEvaluationResultValue(evaluationResult);
            if (evaluationResultValue.isError)
                textWriter = Console.Error;

            resultBuilder.AppendLine(evaluationResultValue.value);
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

    private (string value, bool isError) GetEvaluationResultValue(EvaluationResult evaluationResult)
    {
        try
        {
            var value = evaluationResult.Value?.ToString() ?? "";

            return (value, isError: false);
        }
        catch (RuntimeException ex)
        {
            var diagnostic = new DiagnosticMessage(ex.Message, ex.StartPosition ?? TextPos.Default, ex.EndPosition ?? TextPos.Default)
            {
                StackTrace = ex.ElkStackTrace,
            };
            var value = diagnostic.ToString(includePosition: false).Trim();

            return (value, isError: true);
        }
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

        _virtualMachine.RootModule.AnalysisStatus = AnalysisStatus.None;
        var evaluationResult = ElkProgram.Evaluate(
            File.ReadAllText(filePath),
            _virtualMachine.RootModule,
            AnalysisScope.OncePerModule,
            _virtualMachine
        );

        if (evaluationResult.Diagnostics.Any())
        {
            foreach (var diagnostic in evaluationResult.Diagnostics)
                Console.Error.WriteLine(diagnostic.ToString().Trim());

            Environment.Exit(1);
        }

        var mainFunction = _virtualMachine.RootModule.FindFunction("main", lookInImports: false);
        if (mainFunction != null)
        {
            _virtualMachine.ExecuteFunction("main", new List<RuntimeObject>(), isRoot: true);
        }

        CallOnExit();
    }
}
