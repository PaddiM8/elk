using System.Diagnostics;
using System.Text;
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
public class StdFunctionBindingsGenerator : ISourceGenerator
{
    /// <summary>
    /// In order to debug this in Rider, set this to true,
    /// rebuild the project, click Run -> Attach Process...
    /// -> the dotnet compiler one.
    /// </summary>
    private readonly bool _useDebugger = false;

    public void Initialize(GeneratorInitializationContext context)
    {
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

        public static partial class FunctionBindings
        {
            private static Dictionary<string, StdFunction> _functions = new()
            {
        """
        );

        var modules = new Dictionary<string, List<string>>();
        foreach (var function in FindMethods(context.Compilation))
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
                        ? $"((RuntimeObject{nullable})"
                        : $"({typeName})"
                );

                sourceBuilder.Append($"args[{i}]");
                if (string.IsNullOrEmpty(nullable))
                    sourceBuilder.Append("!");

                if (isStdType)
                {
                    sourceBuilder.Append(")");

                    // It is not possible to convert a value to RuntimeObject
                    // with As<T>(). Therefore, this method should only be
                    // added if it is not expecting just a RuntimeObject.
                    if (typeName != "RuntimeObject")
                        sourceBuilder.Append($"{nullable}.As<{typeName.TrimEnd('?')}>()");
                }
            }

            sourceBuilder.Append(")");

            if (isVoid)
                sourceBuilder.Append("; return RuntimeNil.Value; }");

            sourceBuilder.Append(") },");
        }

        sourceBuilder.AppendLine();
        sourceBuilder.Append(
            """
            };

            private static Dictionary<string, ImmutableArray<string>> _modules = new()
            {
        """
        );

        foreach (var module in modules)
        {
            var functionNames = string.Join(", ", module.Value.Select(x => $"\"{x}\""));
            sourceBuilder.Append($"\n\t\t{{ {module.Key}, ImmutableArray.Create(new[] {{ {functionNames} }}) }},");
        }

        sourceBuilder.Append(
            """

            };
        }
        """
        );

        context.AddSource("FunctionBindings", SourceText.From(sourceBuilder.ToString(), Encoding.UTF8));
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

    private IEnumerable<StdModuleInfo> FindClasses(Compilation compilation)
    {
        var treesWithAttributes = compilation
            .SyntaxTrees
            .Where(tree =>
                tree
                    .GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Any(p => p.DescendantNodes().OfType<AttributeSyntax>().Any())
            );
        foreach (var tree in treesWithAttributes)
        {
            var declaredClasses = tree
                .GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(cd => cd.DescendantNodes().OfType<AttributeSyntax>().Any());
            foreach (var declaredClass in declaredClasses)
            {
                var attribute = FindAttribute(declaredClass, "ElkModule");
                if (attribute != null)
                {
                    var nameExpr = (LiteralExpressionSyntax)attribute
                        .ArgumentList!
                        .Arguments
                        .First()
                        .Expression;

                    yield return new StdModuleInfo(nameExpr.Token.ValueText, declaredClass);
                }
            }
        }
    }

    private IEnumerable<StdFunctionInfo> FindMethods(Compilation compilation)
    {
        foreach (var module in FindClasses(compilation))
        {
            var declaredMethods = module
                .Syntax
                .Members
                .Where(x => x.IsKind(SyntaxKind.MethodDeclaration))
                .OfType<MethodDeclarationSyntax>();
            foreach (var declaredMethod in declaredMethods)
            {
                var attribute = FindAttribute(declaredMethod, "ElkFunction");
                if (attribute == null)
                    continue;

                var semanticModel = compilation.GetSemanticModel(declaredMethod.SyntaxTree);

                yield return AnalyseMethod(semanticModel, declaredMethod, attribute, module);
            }
        }
    }

    private StdFunctionInfo AnalyseMethod(
        SemanticModel model,
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
            if (typeName == "RuntimeObject" ||
                TypeHasAncestor(model.GetTypeInfo(type).Type!, "RuntimeObject") ||
                typeName is "IEnumerable<RuntimeObject>" or "IIndexable<RuntimeObject>")
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

    private bool TypeHasAncestor(ITypeSymbol typeSymbol, string ancestorName)
    {
        var baseType = typeSymbol.BaseType;
        while (baseType?.Name != ancestorName)
        {
            if (baseType == null)
                break;

            baseType = baseType.BaseType;
        }

        return baseType?.Name == ancestorName;
    }
}