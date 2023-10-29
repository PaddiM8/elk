using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Elk.Generators;

record StdModuleInfo(string Name, ClassDeclarationSyntax Syntax);

// ReSharper disable once NotAccessedPositionalProperty.Global
record StdStructInfo(
    string? ModuleName,
    string StructName,
    int MinArgumentCount,
    int MaxArgumentCount,
    int? VariadicStart,
    List<(string type, string name)> Parameters,
    MethodDeclarationSyntax Syntax);

record StdFunctionInfo(
    string? ModuleName,
    string FunctionName,
    string CallingName,
    string? Documentation,
    int MinArgumentCount,
    int MaxArgumentCount,
    bool HasClosure,
    int? VariadicStart,
    bool ConsumesPipe,
    bool StartsPipeManually,
    List<(string type, string name)> Parameters,
    MethodDeclarationSyntax Syntax);

record ModuleEntry(List<string> StructNames, List<string> FunctionNames);

[Generator]
public class StdBindingsGenerator : ISourceGenerator
{
    /// <summary>
    /// In order to debug this in Rider, set this to true,
    /// rebuild the project, click Run -> Attach Process...
    /// -> the dotnet compiler one.
    /// </summary>
    // ReSharper disable once ConvertToConstant.Local
    private readonly bool _useDebugger = false;
    private readonly HashSet<string> _typeNames = new();
    private const string BaseObjectName = "Elk.Std.DataTypes.RuntimeObject";

    private readonly Dictionary<string, string> _additionalTypes = new()
    {
        { "Iterable", $"System.Collections.Generic.IEnumerable<{BaseObjectName}>" },
        { "Indexable", $"Elk.Std.DataTypes.IIndexable<{BaseObjectName}>" },
    };


    public void Initialize(GeneratorInitializationContext context)
    {
        _typeNames.Add(BaseObjectName);
        foreach (var additionalType in _additionalTypes)
            _typeNames.Add(additionalType.Value);
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (_useDebugger)
            SpinWait.SpinUntil(() => Debugger.IsAttached);

        var sourceBuilder = new StringBuilder(
            """
        using System;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using Elk.Std;
        using Elk.Interpreting;
        using Elk.Std.DataTypes;

        namespace Elk.Std.Bindings;

        public static partial class StdBindings
        {

        """
        );

        // Types
        sourceBuilder.Append(
            """
            private static Dictionary<string, Type> _types = new()
            {
        """
        );
        GenerateTypeEntries(context.Compilation, sourceBuilder);
        sourceBuilder.AppendLine("\n\t};\n");

        // Functions
        var modules = new Dictionary<string, ModuleEntry>();
        sourceBuilder.Append(
            """
            private static Dictionary<string, StdFunction> _functions = new()
            {
        """
        );
        GenerateFunctionEntries(context.Compilation, sourceBuilder, modules);
        sourceBuilder.AppendLine("\n\t};\n");

        // Structs
        sourceBuilder.Append(
            """
            private static Dictionary<string, StdStruct> _structs = new()
            {
        """
        );
        GenerateStructEntries(context.Compilation, sourceBuilder, modules);
        sourceBuilder.AppendLine("\n\t};\n");

        // Modules
        sourceBuilder.Append(
            """
            private static Dictionary<string, (ImmutableArray<string> structNames, ImmutableArray<string> functionNames)> _modules = new()
            {
        """
        );
        GenerateModuleEntries(sourceBuilder, modules);
        sourceBuilder.AppendLine("\n\t};");

        // End
        sourceBuilder.Append('}');
        context.AddSource("StdBindings", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
    }

    private void GenerateTypeEntries(Compilation compilation, StringBuilder sourceBuilder)
    {
        // Iterable, Indexable, etc.
        foreach (var (key, value) in _additionalTypes)
            sourceBuilder.Append($"\n\t\t{{ \"{key}\", typeof({value}) }},");

        foreach (var declaredClass in FindClasses(compilation))
        {
            var attribute = FindAttribute(declaredClass, "ElkType");
            if (attribute == null)
                continue;

            var nameExpr = (LiteralExpressionSyntax)attribute
                .ArgumentList!
                .Arguments
                .First()
                .Expression;
            var typeName = nameExpr.Token.ValueText;
            var classType = GetFullTypeName(compilation, declaredClass);

            _typeNames.Add(classType);
            sourceBuilder.Append($"\n\t\t{{ \"{typeName}\", typeof({classType}) }},");
        }
    }

    private static string GetFullTypeName(Compilation compilation, BaseTypeDeclarationSyntax syntaxNode)
    {
        var name = compilation
            .GetSemanticModel(syntaxNode.SyntaxTree)
            .GetDeclaredSymbol(syntaxNode)?
            .ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
            .Replace("<T>", "<Elk.Std.DataTypes.RuntimeObject>") ?? syntaxNode.ToString();

        return name.StartsWith("global::")
            ? name["global::".Length..]
            : name;
    }

    private static string GetFullTypeName(Compilation compilation, ExpressionSyntax syntaxNode)
    {
        return compilation
            .GetSemanticModel(syntaxNode.SyntaxTree)
            .GetTypeInfo(syntaxNode)
            .Type!
            .ToDisplayString();
    }

    private void GenerateFunctionEntries(
        Compilation compilation,
        StringBuilder sourceBuilder,
        IDictionary<string, ModuleEntry> modules)
    {
        foreach (var function in FindMethods(compilation))
        {
            var fullName = function.ModuleName == null
                ? function.FunctionName
                : $"{function.ModuleName}::{function.FunctionName}";
            sourceBuilder.Append("\n\t\t{ \"");
            sourceBuilder.Append(fullName);

            string moduleName;
            if (string.IsNullOrEmpty(function.ModuleName))
            {
                moduleName = "null";
                if (!modules.TryGetValue("\"*\"", out var moduleEntries))
                    modules.Add("\"*\"", new(new List<string>(), new List<string> { function.FunctionName }));
                else
                    moduleEntries.FunctionNames.Add(function.FunctionName);
            }
            else
            {
                moduleName = $"\"{function.ModuleName}\"";
                if (!modules.TryGetValue(moduleName, out var moduleEntries))
                    modules.Add(moduleName, new(new List<string>(), new List<string> { function.FunctionName }));
                else
                    moduleEntries.FunctionNames.Add(function.FunctionName);
            }

            sourceBuilder.Append("\", new(");
            sourceBuilder.Append($"{moduleName}, ");
            sourceBuilder.Append($"\"{function.FunctionName}\", ");
            var documentation = function.Documentation?
                .Replace("\\", @"\\")
                .Replace("\"", "\\\"")
                .Replace("\n", " ");
            var documentationLiteral = documentation == null
                ? "null"
                : $"\"{documentation}\"";
            sourceBuilder.Append($"{documentationLiteral}, ");
            sourceBuilder.Append($"{function.MinArgumentCount}, ");
            sourceBuilder.Append($"{function.MaxArgumentCount}, ");

            var hasClosure = function.HasClosure
                ? "true"
                : "false";
            var variadicStart = function.VariadicStart.HasValue
                ? function.VariadicStart.Value.ToString()
                : "null";
            var consumesPipe = function.ConsumesPipe
                ? "true"
                : "false";
            var startsPipeManually = function.StartsPipeManually
                ? "true"
                : "false";
            sourceBuilder.Append($"{hasClosure}, ");
            sourceBuilder.Append($"{variadicStart}, ");
            sourceBuilder.Append($"{consumesPipe}, ");
            sourceBuilder.Append($"{startsPipeManually}, ");

            // Parameter list
            GenerateParameterList(sourceBuilder, function.Parameters);
            sourceBuilder.Append(", ");

            // Invocation object
            sourceBuilder.Append("args => ");

            var isVoid = function.Syntax.ReturnType.ToString() == "void";
            if (isVoid)
                sourceBuilder.Append("{ ");

            sourceBuilder.Append($"{function.CallingName}(");

            foreach (var (parameter, i) in function.Syntax.ParameterList.Parameters.WithIndex())
            {
                if (i != 0)
                    sourceBuilder.Append(", ");

                var typeName = GetFullTypeName(compilation, parameter.Type!);
                var isStdType = parameter.Type!.ToString().StartsWith("Runtime");
                var nullable = parameter.Type is NullableTypeSyntax
                    ? "?"
                    : "";
                sourceBuilder.Append(
                    isStdType
                        ? $"(({BaseObjectName}{nullable})"
                        : $"({typeName}{nullable})"
                );

                sourceBuilder.Append($"args[{i}]");
                if (string.IsNullOrEmpty(nullable))
                    sourceBuilder.Append('!');

                if (isStdType)
                {
                    sourceBuilder.Append(')');

                    // It is not possible to convert a value to RuntimeObject
                    // with As<T>(). Therefore, this method should only be
                    // added if it is not expecting just a RuntimeObject.
                    if (typeName != BaseObjectName)
                        sourceBuilder.Append($"{nullable}.As<{typeName}>()");
                }
            }

            sourceBuilder.Append(')');

            if (isVoid)
                sourceBuilder.Append("; return RuntimeNil.Value; }");

            sourceBuilder.Append(") },");
        }
    }

    private void GenerateParameterList(StringBuilder sourceBuilder, IEnumerable<(string type, string name)> parameters)
    {
        sourceBuilder.Append("ImmutableArray.Create(new StdFunctionParameter[] { ");
        foreach (var (parameter, i) in parameters.WithIndex())
        {
            if (i != 0)
                sourceBuilder.Append(", ");

            var additional = "";

            // Nullable
            if (parameter.type.EndsWith("?"))
                additional = ", true";

            // Closure
            if (parameter.type.StartsWith("System.Func<") || parameter.type.StartsWith("System.Action<"))
                additional = ", false, true";

            sourceBuilder.Append($"new(typeof({parameter.type.TrimEnd('?')}), \"{parameter.name}\"{additional})");
        }

        sourceBuilder.Append(" })");
    }

    private void GenerateStructEntries(
        Compilation compilation,
        StringBuilder sourceBuilder,
        Dictionary<string, ModuleEntry> modules)
    {
        foreach (var structInfo in FindStructs(compilation))
        {
            var fullName = $"{structInfo.ModuleName}::{structInfo.StructName}";
            sourceBuilder.Append($"\n\t\t{{ \"{fullName}\", ");

            var moduleName = $"\"{structInfo.ModuleName}\"";
            if (!modules.TryGetValue(moduleName, out var moduleEntries))
                modules.Add(moduleName, new(new List<string> { structInfo.StructName }, new List<string>()));
            else
                moduleEntries.StructNames.Add(structInfo.StructName);

            sourceBuilder.Append("new(");
            sourceBuilder.Append($"{moduleName}, ");
            sourceBuilder.Append($"\"{structInfo.StructName}\", ");
            sourceBuilder.Append($"{structInfo.MinArgumentCount}, ");
            sourceBuilder.Append($"{structInfo.MaxArgumentCount}, ");

            var variadicStart = structInfo.VariadicStart.HasValue
                ? structInfo.VariadicStart.Value.ToString()
                : "null";
            sourceBuilder.Append($"{variadicStart}, ");

            GenerateParameterList(sourceBuilder, structInfo.Parameters);
            sourceBuilder.Append(") },");
        }
    }

    private void GenerateModuleEntries(StringBuilder sourceBuilder, Dictionary<string, ModuleEntry> modules)
    {
        foreach (var module in modules)
        {
            var structNames = string.Join(", ", module.Value.StructNames.Select(x => $"\"{x}\""));
            var functionNames = string.Join(", ", module.Value.FunctionNames.Select(x => $"\"{x}\""));
            var structArrayType = module.Value.StructNames.Any()
                ? ""
                : " string";
            var functionArrayType = module.Value.FunctionNames.Any()
                ? ""
                : " string";
            sourceBuilder.Append($"\n\t\t{{ {module.Key}, (");
            sourceBuilder.Append($"ImmutableArray.Create(new{structArrayType}[] {{ {structNames} }}), ");
            sourceBuilder.Append($"ImmutableArray.Create(new{functionArrayType}[] {{ {functionNames} }})");
            sourceBuilder.Append(") },");
        }
    }

    private AttributeSyntax? FindAttribute(SyntaxNode node, string name)
    {
        return node.DescendantNodes()
            .OfType<AttributeSyntax>()
            .FirstOrDefault(
                a => a
                    .DescendantTokens()
                    .Any(b => b.IsKind(SyntaxKind.IdentifierToken) && b.Text == name)
            );
    }

    private IEnumerable<ClassDeclarationSyntax> FindClasses(Compilation compilation)
    {
        return compilation
            .SyntaxTrees
            .SelectMany(tree =>
                tree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Where(p => p.DescendantNodes().OfType<AttributeSyntax>().Any())
            );
    }

    private IEnumerable<StdModuleInfo> FindModules(Compilation compilation)
    {
        return from declaredClass in FindClasses(compilation)
            let attribute = FindAttribute(declaredClass, "ElkModule")
            where attribute != null
            let nameExpr = (LiteralExpressionSyntax)attribute
                .ArgumentList!
                .Arguments
                .First()
                .Expression
            select new StdModuleInfo(nameExpr.Token.ValueText, declaredClass);
    }

    private IEnumerable<StdStructInfo> FindStructs(Compilation compilation)
    {
        return from module in FindModules(compilation)
            let declaredMethods = module
                .Syntax
                .Members
                .Where(x => x.IsKind(SyntaxKind.MethodDeclaration))
                .OfType<MethodDeclarationSyntax>()
            from declaredMethod in declaredMethods
            let attribute = FindAttribute(declaredMethod, "ElkStruct")
            where attribute != null
            select AnalyseStruct(compilation, declaredMethod, attribute, module);
    }

    private IEnumerable<StdFunctionInfo> FindMethods(Compilation compilation)
    {
        return from module in FindModules(compilation)
            let declaredMethods = module
                .Syntax
                .Members
                .Where(x => x.IsKind(SyntaxKind.MethodDeclaration))
                .OfType<MethodDeclarationSyntax>()
            from declaredMethod in declaredMethods
            let attribute = FindAttribute(declaredMethod, "ElkFunction")
            where attribute != null
            select AnalyseMethod(compilation, declaredMethod, attribute, module);
    }

    private StdStructInfo AnalyseStruct(
        Compilation compilation,
        MethodDeclarationSyntax methodSyntax,
        AttributeSyntax attribute,
        StdModuleInfo module)
    {
        var attributeArguments = attribute.ArgumentList!.Arguments;
        var name = ((LiteralExpressionSyntax)attributeArguments[0].Expression).Token.ValueText;
        var parameters = AnalyseParameters(
            compilation,
            methodSyntax.ParameterList.Parameters,
            out var minArgumentCount,
            out var maxArgumentCount,
            out _,
            out var variadicStart
        );

        return new StdStructInfo(
            module.Name,
            name,
            minArgumentCount,
            maxArgumentCount,
            variadicStart,
            parameters,
            methodSyntax
        );
    }

    private StdFunctionInfo AnalyseMethod(
        Compilation compilation,
        MethodDeclarationSyntax methodSyntax,
        AttributeSyntax attribute,
        StdModuleInfo module)
    {
        // Attribute
        var attributeArguments = attribute.ArgumentList!.Arguments;
        var reachableEverywhere = false;
        var consumesPipe = false;
        var startsPipeManually = false;
        if (attributeArguments.Count > 1)
        {
            var reachabilityExpr = (MemberAccessExpressionSyntax)attributeArguments[1].Expression;
            var reachability = reachabilityExpr.Name.Identifier.Text;
            reachableEverywhere = reachability == "Everywhere";
            consumesPipe = attributeArguments
                .FirstOrDefault(x => x.NameEquals?.Name.Identifier.Text == "ConsumesPipe")?
                .Expression
                .GetText()
                .ToString() == "true";
            startsPipeManually = attributeArguments
                .FirstOrDefault(x => x.NameEquals?.Name.Identifier.Text == "StartsPipeManually")?
                .Expression
                .GetText()
                .ToString() == "true";
        }

        var name = ((LiteralExpressionSyntax)attributeArguments[0].Expression).Token.ValueText;
        var parameters = AnalyseParameters(
            compilation,
            methodSyntax.ParameterList.Parameters,
            out var minArgumentCount,
            out var maxArgumentCount,
            out var hasClosure,
            out var variadicStart
        );

        var namespacePath = compilation
            .GetSemanticModel(methodSyntax.SyntaxTree)
            .GetDeclaredSymbol(methodSyntax)?
            .ContainingNamespace
            .ToDisplayString();

        var documentationXml = compilation
            .GetSemanticModel(methodSyntax.SyntaxTree)
            .GetDeclaredSymbol(methodSyntax)!
            .GetDocumentationCommentXml();
        var documentation = "";
        if (!string.IsNullOrEmpty(documentationXml))
        {
            var document = new XmlDocument();
            document.LoadXml(documentationXml);
            var summary = document.DocumentElement?
                .SelectSingleNode("/member/summary")?
                .InnerText;
            var returns = document.DocumentElement?
                .SelectSingleNode("/member/returns")?
                .InnerText;
            if (summary?.EndsWith('.') is false)
                summary += ".";

            documentation = $"{summary} returns: {returns}".Trim();
        }

        return new StdFunctionInfo(
            reachableEverywhere ? null : module.Name,
            name,
            $"{namespacePath}.{module.Syntax.Identifier.Text}.{methodSyntax.Identifier.Text}",
            documentation,
            minArgumentCount,
            maxArgumentCount,
            hasClosure,
            variadicStart,
            consumesPipe,
            startsPipeManually,
            parameters,
            methodSyntax
        );
    }

    private List<(string, string)> AnalyseParameters(
        Compilation compilation,
        SeparatedSyntaxList<ParameterSyntax> parameterSyntaxes,
        out int minArgumentCount,
        out int maxArgumentCount,
        out bool hasClosure,
        out int? variadicStart)
    {
        var parameters = new List<(string, string)>();
        minArgumentCount = 0;
        maxArgumentCount = 0;
        variadicStart = null;
        hasClosure = false;
        foreach (var (parameter, i) in parameterSyntaxes.WithIndex())
        {
            var type = parameter.Type!;
            var fullTypeName = GetFullTypeName(compilation, type);
            if (_typeNames.Contains(fullTypeName))
            {
                var isVariadic = FindAttribute(parameter, "ElkVariadic") != null;
                if (isVariadic)
                    variadicStart = i;

                if (parameter.Default == null && !isVariadic)
                    minArgumentCount++;

                maxArgumentCount++;
            }

            var unqualifiedTypeName = type.ToString();
            if (unqualifiedTypeName.StartsWith("Func<") || unqualifiedTypeName.StartsWith("Action<"))
                hasClosure = true;

            var nullability = unqualifiedTypeName.EndsWith("?")
                ? "?"
                : "";
            parameters.Add((fullTypeName + nullability, parameter.Identifier.Text));
        }

        if (variadicStart.HasValue)
            maxArgumentCount = int.MaxValue;

        return parameters;
    }
}