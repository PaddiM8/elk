#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Analysis;
using Elk.Scoping;
using Elk.Vm;
using NUnit.Framework;
using Path = System.IO.Path;

#endregion

namespace Elk.Tests;

public class IntegrationTests
{
    [TestCaseSource(nameof(ElkFiles))]
    public void RunIntegrationTests(string filePath)
    {
        var virtualMachine = new VirtualMachine(
            new RootModuleScope(filePath, null),
            new VirtualMachineOptions()
        );
        var result = ElkProgram.Evaluate(
            File.ReadAllText(filePath),
            virtualMachine.RootModule,
            AnalysisScope.OncePerModule,
            virtualMachine
        );
        Assert.IsEmpty(result.Diagnostics);
    }

    public static IEnumerable<string> ElkFiles()
    {
        var dataPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles");
        var examplesPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../../examples");

        return new[]
        {
            Directory.GetFiles(dataPath, "*.elk", SearchOption.AllDirectories),
            Directory.GetFiles(Path.Combine(examplesPath, "advent-of-code-2022"), "*.elk", SearchOption.AllDirectories),
        }.SelectMany(x => x);
    }
}