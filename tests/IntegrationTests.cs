#region

using System.Collections.Generic;
using System.IO;
using Elk.Interpreting;
using NUnit.Framework;

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

        Assert.DoesNotThrow(() => interpreter.Interpret(File.ReadAllText(filePath)));
    }

    public static IEnumerable<string> ElkFiles()
    {
        string dataPath = Path.Combine(TestContext.CurrentContext.TestDirectory, "TestFiles");

        return Directory.GetFiles(dataPath, "*.elk", SearchOption.AllDirectories);
    }
}