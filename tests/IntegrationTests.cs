#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Analysis;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using NUnit.Framework;
using Path = System.IO.Path;

#endregion

namespace Elk.Tests;

public class IntegrationTests
{
    [TestCaseSource(nameof(ElkFiles))]
    public void RunIntegrationTests(string filePath)
    {
        var interpreter = new Interpreter(filePath)
        {
            PrintErrors = false,
        };

        Assert.DoesNotThrow(() =>
        {
            try
            {
                ElkProgram.Evaluate(
                    File.ReadAllText(filePath),
                    interpreter.CurrentModule,
                    AnalysisScope.OncePerModule,
                    interpreter
                );
            }
            catch (RuntimeException e)
            {
                throw new RuntimeException($"{e.Position} {e.Message}");
            }
        });
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