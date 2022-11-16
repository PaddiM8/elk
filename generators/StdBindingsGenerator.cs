using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Elk.Generators;

record StdModuleInfo(string Name, ClassDeclarationSyntax Syntax);

record StdFunctionInfo(
    string? ModuleName,
    string FunctionName,
    string CallingName,
    int MinArgumentCount,
    int MaxArgumentCount,
    bool HasClosure,
    int? VariadicStart,
    List<string> Parameters,
    MethodDeclarationSyntax Syntax);

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
        var modules = new Dictionary<string, List<string>>();
        sourceBuilder.Append(
            """
            private static Dictionary<string, StdFunction> _functions = new()
            {
        """
        );
        GenerateFunctionEntries(context.Compilation, sourceBuilder, modules);
        sourceBuilder.AppendLine("\n\t};\n");

        // Modules
        sourceBuilder.Append(
            """
            private static Dictionary<string, ImmutableArray<string>> _modules = new()
            {
        """
        );
        GenerateModuleEntries(sourceBuilder, modules);
        sourceBuilder.AppendLine("\n\t};");

        // End
        sourceBuilder.Append("}");
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
        IDictionary<string, List<string>> modules)
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
                if (!modules.TryGetValue(moduleName, out var moduleFunctions))
                    modules.Add(moduleName, new List<string> { function.FunctionName });
                else
                    moduleFunctions.Add(function.FunctionName);
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
            sourceBuilder.Append("ImmutableArray.Create(new StdFunctionParameter[] { ");
            foreach (var (parameter, i) in function.Parameters.WithIndex())
            {
                if (i != 0)
                    sourceBuilder.Append(", ");

                string additional = "";

                // Nullable
                if (parameter.EndsWith("?"))
                    additional = ", true";

                // Closure
                if (parameter.StartsWith("Func<"))
                    additional = ", false, true";

                sourceBuilder.Append($"new(typeof({parameter.TrimEnd('?')}){additional})");
            }

            sourceBuilder.Append(" }), ");

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
                    if (typeName != BaseObjectName)
                        sourceBuilder.Append($"{nullable}.As<{typeName.TrimEnd('?')}>()");
                }
            }

            sourceBuilder.Append(')');

            if (isVoid)
                sourceBuilder.Append("; return RuntimeNil.Value; }");

            sourceBuilder.Append(") },");
        }
    }

    private void GenerateModuleEntries(StringBuilder sourceBuilder, Dictionary<string, List<string>> modules)
    {
        foreach (var module in modules)
        {
            var functionNames = string.Join(", ", module.Value.Select(x => $"\"{x}\""));
            sourceBuilder.Append($"\n\t\t{{ {module.Key}, ImmutableArray.Create(new[] {{ {functionNames} }}) }},");
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

        // Parameters
        var parameters = new List<string>();
        int minArgumentCount = 0;
        int maxArgumentCount = 0;
        int? variadicStart = null;
        bool hasClosure = false;
        foreach (var (parameter, i) in methodSyntax.ParameterList.Parameters.WithIndex())
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

            parameters.Add(type.ToString());
        }

        if (variadicStart.HasValue)
            maxArgumentCount = int.MaxValue;

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
}