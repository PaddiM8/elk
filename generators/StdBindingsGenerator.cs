using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Elk.Generators;

record StdModuleInfo(string Name, ClassDeclarationSyntax Syntax);

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
    int MinArgumentCount,
    int MaxArgumentCount,
    bool HasClosure,
    int? VariadicStart,
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
    private const string BaseObjectName = "RuntimeObject";

    private readonly Dictionary<string, string> _additionalTypes = new()
    {
        { "Iterable", $"IEnumerable<{BaseObjectName}>" },
        { "Indexable", $"IIndexable<{BaseObjectName}>" },
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

            var classType = declaredClass.Identifier.ValueText;

            _typeNames.Add(classType);
            sourceBuilder.Append($"\n\t\t{{ \"{typeName}\", typeof({classType}) }},");
        }
    }

    private void GenerateFunctionEntries(
        Compilation compilation,
        StringBuilder sourceBuilder,
        IDictionary<string, ModuleEntry> modules)
    {
        foreach (var function in FindMethods(compilation))
        {
            string fullName = function.ModuleName == null
                ? function.FunctionName
                : $"{function.ModuleName}::{function.FunctionName}";
            sourceBuilder.Append("\n\t\t{ \"");
            sourceBuilder.Append(fullName);

            string moduleName;
            if (string.IsNullOrEmpty(function.ModuleName))
            {
                moduleName = "null";
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
            sourceBuilder.Append($"{function.MinArgumentCount}, ");
            sourceBuilder.Append($"{function.MaxArgumentCount}, ");

            string hasClosure = function.HasClosure
                ? "true"
                : "false";
            string variadicStart = function.VariadicStart.HasValue
                ? function.VariadicStart.Value.ToString()
                : "null";
            sourceBuilder.Append($"{hasClosure}, ");
            sourceBuilder.Append($"{variadicStart}, ");

            // Parameter list
            GenerateParameterList(sourceBuilder, function.Parameters);
            sourceBuilder.Append(", ");

            // Invocation object
            sourceBuilder.Append("args => ");

            bool isVoid = function.Syntax.ReturnType.ToString() == "void";
            if (isVoid)
                sourceBuilder.Append("{ ");

            sourceBuilder.Append($"{function.CallingName}(");

            foreach (var (parameter, i) in function.Syntax.ParameterList.Parameters.WithIndex())
            {
                if (i != 0)
                    sourceBuilder.Append(", ");

                string typeName = parameter.Type!.ToString();
                bool isStdType = typeName.StartsWith("Runtime");
                string nullable = parameter.Type is NullableTypeSyntax
                    ? "?"
                    : "";
                sourceBuilder.Append(
                    isStdType
                        ? $"(({BaseObjectName}{nullable})"
                        : $"({typeName})"
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
                    if (typeName.TrimEnd('?') != BaseObjectName)
                        sourceBuilder.Append($"{nullable}.As<{typeName.TrimEnd('?')}>()");
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

            string additional = "";

            // Nullable
            if (parameter.type.EndsWith("?"))
                additional = ", true";

            // Closure
            if (parameter.type.StartsWith("Func<") || parameter.type.StartsWith("Action<"))
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
            string fullName = $"{structInfo.ModuleName}::{structInfo.StructName}";
            sourceBuilder.Append($"\n\t\t{{ \"{fullName}\", ");

            string moduleName = $"\"{structInfo.ModuleName}\"";
            if (!modules.TryGetValue(moduleName, out var moduleEntries))
                modules.Add(moduleName, new(new List<string> { structInfo.StructName }, new List<string>()));
            else
                moduleEntries.StructNames.Add(structInfo.StructName);

            sourceBuilder.Append("new(");
            sourceBuilder.Append($"{moduleName}, ");
            sourceBuilder.Append($"\"{structInfo.StructName}\", ");
            sourceBuilder.Append($"{structInfo.MinArgumentCount}, ");
            sourceBuilder.Append($"{structInfo.MaxArgumentCount}, ");

            string variadicStart = structInfo.VariadicStart.HasValue
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
            string structArrayType = module.Value.StructNames.Any()
                ? ""
                : " string";
            string functionArrayType = module.Value.FunctionNames.Any()
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
            select AnalyseStruct(declaredMethod, attribute, module);
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
            select AnalyseMethod(declaredMethod, attribute, module);
    }

    private StdStructInfo AnalyseStruct(
        MethodDeclarationSyntax methodSyntax,
        AttributeSyntax attribute,
        StdModuleInfo module)
    {
        var attributeArguments = attribute.ArgumentList!.Arguments;
        var name = ((LiteralExpressionSyntax)attributeArguments[0].Expression).Token.ValueText;
        var parameters = AnalyseParameters(
            methodSyntax.ParameterList.Parameters,
            out int minArgumentCount,
            out int maxArgumentCount,
            out int? variadicStart,
            out _
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
        MethodDeclarationSyntax methodSyntax,
        AttributeSyntax attribute,
        StdModuleInfo module)
    {
        // Attribute
        var attributeArguments = attribute.ArgumentList!.Arguments;
        bool reachableEverywhere = false;
        if (attributeArguments.Count > 1)
        {
            var reachabilityExpr = (MemberAccessExpressionSyntax)attributeArguments[1].Expression;
            string reachability = reachabilityExpr.Name.Identifier.Text;
            reachableEverywhere = reachability == "Everywhere";
        }

        var name = ((LiteralExpressionSyntax)attributeArguments[0].Expression).Token.ValueText;
        var parameters = AnalyseParameters(
            methodSyntax.ParameterList.Parameters,
            out int minArgumentCount,
            out int maxArgumentCount,
            out int? variadicStart,
            out bool hasClosure
        );

        return new StdFunctionInfo(
            reachableEverywhere ? null : module.Name,
            name,
            $"{module.Syntax.Identifier.Text}.{methodSyntax.Identifier.Text}",
            minArgumentCount,
            maxArgumentCount,
            hasClosure,
            variadicStart,
            parameters,
            methodSyntax
        );
    }

    private List<(string, string)> AnalyseParameters(
        SeparatedSyntaxList<ParameterSyntax> parameterSyntaxes,
        out int minArgumentCount,
        out int maxArgumentCount,
        out int? variadicStart,
        out bool hasClosure)
    {
        var parameters = new List<(string, string)>();
        minArgumentCount = 0;
        maxArgumentCount = 0;
        variadicStart = null;
        hasClosure = false;
        foreach (var (parameter, i) in parameterSyntaxes.WithIndex())
        {
            var type = parameter.Type!;
            string typeName = type.ToString().TrimEnd('?');
            if (_typeNames.Contains(typeName))
            {
                bool isVariadic = FindAttribute(parameter, "ElkVariadic") != null;
                if (isVariadic)
                    variadicStart = i;

                if (parameter.Default == null && !isVariadic)
                    minArgumentCount++;

                maxArgumentCount++;
            }

            if (typeName.StartsWith("Func<"))
                hasClosure = true;

            parameters.Add((type.ToString(), parameter.Identifier.Text));
        }

        if (variadicStart.HasValue)
            maxArgumentCount = int.MaxValue;

        return parameters;
    }
}