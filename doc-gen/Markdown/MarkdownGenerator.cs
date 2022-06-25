using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Elk.DocGen.Markdown;

public class MarkdownGenerator
{
    public static void Generate(StdInfo stdInfo)
    {
        const string dir = "out";
        if (Directory.Exists(dir))
            Directory.Delete(dir, true);

        Directory.CreateDirectory(dir);

        var names = new List<(string, string)> { ("Globals", "globals.md") };
        foreach (var module in stdInfo.Modules)
        {
            string title = $"{module.DisplayName} ({module.Name})";
            var moduleString = new StringBuilder();
            moduleString.AppendLine($"# {title}");

            foreach (var function in module.Functions)
                moduleString.AppendLine(GenerateFunction(function));

            if (module.Functions.Any())
            {
                string fileName = TitleToFileName(title);
                names.Add((title, fileName));
                File.WriteAllText(
                    Path.Combine(dir, fileName),
                    moduleString.ToString()
                );
            }
        }

        var globalsModuleString = new StringBuilder();
        globalsModuleString.AppendLine("# Globals");

        var globals = stdInfo.GlobalFunctions.OrderBy(x => x.Name);
        foreach (var function in globals)
            globalsModuleString.AppendLine(GenerateFunction(function));

        File.WriteAllText(
            Path.Combine(dir, "globals.md"),
            globalsModuleString.ToString()
        );

        GenerateGitBookSummary(names, dir);
    }

    private static string GenerateFunction(FunctionInfo functionInfo)
    {
        var functionString = new StringBuilder();
        var parameters = functionInfo.Parameters;

        // Title
        functionString.Append($"## {functionInfo.Name}(");
        string? signatureWithoutOptionals = null;
        foreach (var parameter in parameters)
        {
            if (signatureWithoutOptionals == null && parameter.IsOptional)
            {
                signatureWithoutOptionals = functionString.ToString()[3..]
                    .RemoveTrailing(", ") + ")";
            }

            functionString.Append(parameter.Name);

            var typeName = parameter.ValueInfo.TypeName;
            if (typeName != null && typeName != "*" && parameter.ValueInfo.HasStaticType)
                functionString.Append($": {typeName}");

            functionString.Append(", ");
        }

        if (signatureWithoutOptionals != null)
        {
            functionString.Insert(3, $"{signatureWithoutOptionals}<br>");
        }

        // Remove the trailing comma
        if (parameters.Any())
            functionString.Remove(functionString.Length - 2, 2);
        functionString.AppendLine(")");
        functionString.AppendLine();

        // Parameter table
        var table = new MarkdownTable("Parameter", "Type", "Description");
        foreach (var (name, valueInfo, isOptional) in parameters)
        {
            table.AddRow(
                isOptional ? $"(optional) {name}" : name,
                valueInfo.TypeName ?? "*",
                valueInfo.Description ?? ""
            );
        }

        if (table.HasRows)
            functionString.AppendLine(table.ToString());

        // Summary
        if (functionInfo.Summary != null)
        {
            functionString.AppendLine(functionInfo.Summary);
            functionString.AppendLine();
        }

        // Returns
        functionString.AppendLine("### Returns\n");

        if (functionInfo.ReturnValue.TypeName == null)
        {
            functionString.AppendLine("(nil)");
        }
        else
        {
            var returnTypeName = functionInfo.ReturnValue.TypeName;
            if (returnTypeName != null)
                functionString.Append($"({returnTypeName}) ");

            string? description = functionInfo.ReturnValue.Description;
            if (!string.IsNullOrEmpty(description))
                functionString.Append(description);

            functionString.AppendLine();
        }

        // Example
        if (functionInfo.Example != null)
        {
            functionString.AppendLine("\n### Example\n");
            string lang = functionInfo.Example.Contains("#")
                ? "nim"
                : "rust";
            functionString.AppendLine($"```{lang}");
            functionString.AppendLine(functionInfo.Example);
            functionString.AppendLine("```");
        }

        return functionString.ToString();
    }

    private static void GenerateGitBookSummary(IEnumerable<(string name, string fileName)> names, string dir)
    {
        var builder = new StringBuilder();
        foreach (var (name, fileName) in names)
        {
            builder.AppendLine($"* [{name}](standard-library/{fileName})");
        }

        File.WriteAllText(Path.Combine(dir, "SUMMARY.md"), builder.ToString());
    }

    private static string TitleToFileName(string title)
        => title.ToLower()
            .Replace(" ", "-")
            .Replace("(", "")
            .Replace(")", "") + ".md";
}