using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Elk.DocGen.Markdown;

public class MarkdownGenerator
{
    public static void Generate(StdInfo stdInfo, string outDirectory)
    {
        if (Directory.Exists(outDirectory))
            Directory.Delete(outDirectory, true);

        Directory.CreateDirectory(outDirectory);

        var modules = new List<ModuleInfo>
        {
            new("built-in", "Built-in", stdInfo.GlobalFunctions),
        };
        modules.AddRange(stdInfo.Modules);

        foreach (var module in modules)
        {
            if (!module.Functions.Any())
                continue;

            var isBuiltIn = module.DisplayName == "Built-in";
            var title = isBuiltIn
                ? "Built-in"
                : $"({module.Name})";
            var folderPath = TitleToFolderName(title);
            Directory.CreateDirectory(Path.Combine(outDirectory, folderPath));

            var indexBuilder = new StringBuilder();
            indexBuilder.AppendLine($"# {module.Name}");

            foreach (var function in module.Functions.OrderBy(x => x.Name))
            {
                var functionModule = isBuiltIn
                    ? null
                    : module.Name;
                var functionName = function.Name == "index"
                    ? "_index"
                    : function.Name;
                File.WriteAllText(
                    Path.Combine(outDirectory, folderPath, functionName) + ".md",
                    GenerateFunction(function, functionModule)
                );
                indexBuilder.AppendLine($"* [{functionName}](/std/{module.Name}/{functionName})");
            }

            File.WriteAllText(
                Path.Combine(outDirectory, folderPath, "index.md"),
                indexBuilder.ToString()
            );
        }

        var sidebar = modules
            .Where(module => module.Functions.Any())
            .OrderBy(x => x.Name == "built-in" ? "AAA" : x.Name)
            .Select(module =>
            {
                var items = module.Functions
                    .OrderBy(x => x.Name)
                    .Select(function => new
                    {
                        text = function.Name == "index"
                            ? "_index"
                            : function.Name,
                        link = $"/std/{module.Name}/{function.Name}",
                    });

                return new
                {
                    text = module.Name,
                    collapsed = true,
                    items,
                };
            });

        File.WriteAllText(
            Path.Combine(outDirectory, "entries.json"),
            JsonConvert.SerializeObject(sidebar)
        );
        File.WriteAllText(
            Path.Combine(outDirectory, "index.md"),
            "# Standard Library"
        );
    }

    private static string GenerateFunction(FunctionInfo functionInfo, string? moduleName)
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
                signatureWithoutOptionals = titleString.ToString()
                    .RemoveTrailing(", ") + "\\)";
            }

            titleString.Append(parameter.Name);

            var typeName = parameter.ValueInfo.TypeName;
            if (typeName != null && typeName != "*" && parameter.ValueInfo.HasStaticType)
                titleString.Append($": {typeName}");
            if (parameter.IsVariadic)
                titleString.Append("...");

            titleString.Append(", ");
        }

        if (signatureWithoutOptionals != null)
        {
            titleString.Insert(0, $"{signatureWithoutOptionals}<br>");
        }

        // Remove the trailing comma
        if (parameters.Any())
            titleString.Remove(titleString.Length - 2, 2);
        titleString.Append("\\)");

        const string alphabet = "abc";
        if (functionInfo.Closure != null)
            titleString.Append($" => {string.Join(", ", alphabet[..functionInfo.Closure.ParameterCount])}");

        var functionString = new StringBuilder();
        if (moduleName != null)
            moduleName += "::";
        functionString.AppendLine($"# {moduleName}{functionInfo.Name}");
        functionString.AppendLine($"## {titleString}\n\n");

        // Parameter table
        var table = new MarkdownTable("Parameter", "Type", "Description");
        foreach (var (name, valueInfo, isOptional, isVariadic) in parameters)
        {
            var firstColumn = isOptional ? $"(optional) {name}" : name;
            var typeName = valueInfo.TypeName ?? "*";
            if (isVariadic)
            {
                firstColumn = $"(variadic) {name}";
                typeName = "*";
            }

            table.AddRow(
                firstColumn,
                typeName,
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

            var description = functionInfo.ReturnValue.Description;
            if (!string.IsNullOrEmpty(description))
                functionString.Append(description);

            functionString.AppendLine();
        }

        // Example
        if (functionInfo.Example != null)
        {
            functionString.AppendLine("\n## Example\n");
            functionString.AppendLine("```elk");
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