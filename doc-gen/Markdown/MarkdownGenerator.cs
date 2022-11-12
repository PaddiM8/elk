using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Elk.DocGen.Markdown;

public class MarkdownGenerator
{
    public static void Generate(StdInfo stdInfo)
    {
        const string outDir = "out";
        const string dir = $"{outDir}/standard-library";
        if (Directory.Exists(outDir))
            Directory.Delete(outDir, true);

        Directory.CreateDirectory(dir);

        var modules = new List<ModuleInfo>
        {
            new("built-in", "Built-in", stdInfo.GlobalFunctions),
        };
        modules.AddRange(stdInfo.Modules);

        var summary = new StringBuilder();
        foreach (var module in modules)
        {
            if (!module.Functions.Any())
                continue;

            bool isBuiltIn = module.DisplayName == "Built-in";
            string title = isBuiltIn
                ? "Built-in"
                : $"{module.DisplayName} ({module.Name})";
            string description = isBuiltIn
                ? "# Built-in\nThese functions do not belong to a module and are always available."
                : $"# {title}\nFunctions in this module can be accessed by with the syntax {module.Name}::functionName or by importing the function from the {module.Name} module.";
            string folderPath = TitleToFolderName(title);

            Directory.CreateDirectory(Path.Combine(dir, folderPath));
            File.WriteAllText(Path.Combine(dir, $"{folderPath}/README.md"), description);
            summary.AppendLine($"* [{title}](standard-library/{folderPath}/README.md)");

            foreach (var function in module.Functions.OrderBy(x => x.Name))
            {
                File.WriteAllText(
                    Path.Combine(dir, folderPath, function.Name) + ".md",
                    GenerateFunction(function)
                );
                summary.AppendLine($"  * [{function.Name}](standard-library/{folderPath}/{function.Name}.md)");
            }
        }

        File.WriteAllText(Path.Combine(outDir, "SUMMARY.md"), summary.ToString());
    }

    private static string GenerateFunction(FunctionInfo functionInfo)
    {
        var parameters = functionInfo.Parameters;

        // Title
        var titleString = new StringBuilder();
        titleString.Append($"{functionInfo.Name}\\(");

        string? signatureWithoutOptionals = null;
        foreach (var parameter in parameters)
        {
            if (signatureWithoutOptionals == null && parameter.IsOptional)
            {
                signatureWithoutOptionals = titleString.ToString()[3..]
                    .RemoveTrailing(", ") + "\\)";
            }

            titleString.Append(parameter.Name);

            var typeName = parameter.ValueInfo.TypeName;
            if (typeName != null && typeName != "*" && parameter.ValueInfo.HasStaticType)
                titleString.Append($": {typeName}");

            titleString.Append(", ");
        }

        if (signatureWithoutOptionals != null)
        {
            titleString.Insert(3, $"{signatureWithoutOptionals}<br>");
        }

        // Remove the trailing comma
        if (parameters.Any())
            titleString.Remove(titleString.Length - 2, 2);
        titleString.Append("\\)");

        const string alphabet = "abc";
        if (functionInfo.Closure != null)
            titleString.Append($" => {string.Join(", ", alphabet[..functionInfo.Closure.ParameterCount])}");

        var functionString = new StringBuilder();
        functionString.AppendLine($"# {functionInfo.Name}");
        functionString.AppendLine($"## {titleString}\n\n");

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
        functionString.AppendLine("## Returns\n");

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
            functionString.AppendLine("\n## Example\n");
            string lang = functionInfo.Example.Contains('#')
                ? "nim"
                : "rust";
            functionString.AppendLine($"```{lang}");
            functionString.AppendLine(functionInfo.Example);
            functionString.AppendLine("```");
        }

        // Error
        if (functionInfo.Errors.Count > 0)
        {
            functionString.AppendLine("\n## Errors\n");

            foreach (var error in functionInfo.Errors)
                functionString.AppendLine($"{error}<br>");
        }

        return functionString.ToString();
    }

    private static string TitleToFolderName(string title)
        => title.ToLower()
            .Replace(" ", "-")
            .Replace("(", "")
            .Replace(")", "");
}