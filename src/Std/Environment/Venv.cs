using System;
using Elk.Analysis;
using Elk.Scoping;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Vm;

namespace Elk.Std.Environment;

[ElkModule("env::venv")]
public static class Venv
{
    /// <summary>
    /// Creates a new Python virtual environment.
    /// </summary>
    /// <param name="path">Where the environment should be created, including the name of the new folder</param>
    [ElkFunction("create")]
    public static void Create(RuntimeString path)
    {
        var virtualMachine = new VirtualMachine(
            new RootModuleScope(null, null),
            new VirtualMachineOptions()
        );
        ElkProgram.Evaluate(
            $"python3 -m venv {path.Value}",
            virtualMachine.RootModule,
            AnalysisScope.OncePerModule,
            virtualMachine
        );

        var venvPath = System.IO.Path.Combine(ShellEnvironment.WorkingDirectory, path.Value);
        var scriptPath = System.IO.Path.Combine(venvPath, "bin/activate.elk");
        var scriptContent = ResourceProvider.ReadFile("venv/activate.elk")!
            .Replace("{{ path }}", venvPath.TrimEnd('/'));
        System.IO.File.WriteAllText(scriptPath, scriptContent);

        Console.WriteLine(
            Ansi.Color(
                $"Created virtual environment. Activate it with `source {path.Value}/bin/activate.elk`",
                "green"
            )
        );
    }
}