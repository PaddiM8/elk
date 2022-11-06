using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk.Analysis;

class Analyser
{
    private static Scope _scope = new ModuleScope();
    private ModuleBag _modules = new();
    private Expr? _lastExpr;

    public static List<Expr> Analyse(List<Expr> ast, ModuleBag modules, ModuleScope scope)
    {
        var analyser = new Analyser
        {
            _modules = modules,
        };

        try
        {
            foreach (var functionSymbol in modules.SelectMany(x => x.Functions).Where(x => !x.Expr.IsAnalysed))
            {
                _scope = functionSymbol.Expr.Module;
                analyser.Next(functionSymbol.Expr);
            }

            _scope = scope;

            return ast
                .Where(expr => expr is not FunctionExpr)
                .Select(expr => analyser.Next(expr))
                .ToList();
        }
        catch (RuntimeException e)
        {
            var pos = analyser._lastExpr?.Position ?? TextPos.Default;
            var error = new DiagnosticInfo(pos.Line, pos.Column, e.Message, pos.FilePath);

            throw new AggregateException(error.ToString(), e)
            {
                Data =
                {
                    ["diagnosticInfo"] = error,
                },
            };
        }
    }

    private Expr Next(Expr expr)
    {
        _lastExpr = expr;

        switch (expr)
        {
            case FunctionExpr e:
                foreach (var parameter in e.Parameters)
                {
                    if (parameter.DefaultValue != null)
                        Next(parameter.DefaultValue!);

                    e.Block.Scope.AddVariable(parameter.Identifier.Value, RuntimeNil.Value);
                }

                var newFunction = new FunctionExpr(
                    e.Identifier,
                    e.Parameters,
                    (BlockExpr)Next(e.Block),
                    e.Module,
                    e.HasClosure,
                    isAnalysed: true
                )
                {
                    IsRoot = e.IsRoot,
                };

                e.Module.AddFunction(newFunction);

                return newFunction;
            case LetExpr e:
                return new LetExpr(e.IdentifierList, Next(e.Value));
            case KeywordExpr e:
                Expr? keywordValue = null;
                if (e.Value != null)
                    keywordValue = Next(e.Value);

                return new KeywordExpr(e.Kind, keywordValue, e.Position)
                {
                    IsRoot = e.IsRoot,
                };
            case IfExpr e:
                var ifCondition = Next(e.Condition);
                var thenBranch = Next(e.ThenBranch);
                Expr? elseBranch = null;
                if (e.ElseBranch != null)
                    elseBranch = Next(e.ElseBranch);

                return new IfExpr(ifCondition, thenBranch, elseBranch)
                {
                    IsRoot = e.IsRoot,
                };
            case ForExpr e:
                var forValue = Next(e.Value);
                foreach (var identifier in e.IdentifierList)
                    e.Branch.Scope.AddVariable(identifier.Value, RuntimeNil.Value);

                var branch = (BlockExpr)Next(e.Branch);

                return new ForExpr(e.IdentifierList, forValue, branch)
                {
                    IsRoot = e.IsRoot,
                };
            case WhileExpr e:
                var whileCondition = Next(e.Condition);
                var whileBranch = (BlockExpr)Next(e.Branch);

                return new WhileExpr(whileCondition, whileBranch)
                {
                    IsRoot = e.IsRoot,
                };
            case TupleExpr e:
                var tupleValues = e.Values.Select(Next).ToList();

                return new TupleExpr(tupleValues, e.Position)
                {
                    IsRoot = e.IsRoot,
                };
            case ListExpr e:
                var listValues = e.Values.Select(Next).ToList();

                return new ListExpr(listValues, e.Position)
                {
                    IsRoot = e.IsRoot,
                };
            case DictionaryExpr e:
                foreach (var (_, value) in e.Entries)
                    Next(value);
                var dictEntries = e.Entries
                    .Select(x => (x.Item1, Next(x.Item2)))
                    .ToList();

                return new DictionaryExpr(dictEntries, e.Position)
                {
                    IsRoot = e.IsRoot,
                };
            case BlockExpr e:
                _scope = e.Scope;
                var blockExpressions = e.Expressions.Select(Next).ToList();
                var newExpr = new BlockExpr(
                    blockExpressions,
                    e.ParentStructureKind,
                    e.Position,
                    e.Scope
                )
                {
                    IsRoot = e.IsRoot,
                };
                _scope = _scope.Parent!;

                return newExpr;
            case LiteralExpr e:
                return Visit(e);
            case StringInterpolationExpr e:
                var parts = e.Parts.Select(Next).ToList();

                return new StringInterpolationExpr(parts, e.Position)
                {
                    IsRoot = e.IsRoot,
                };
            case BinaryExpr e:
                return new BinaryExpr(Next(e.Left), e.Operator, Next(e.Right))
                {
                    IsRoot = e.IsRoot,
                };
            case UnaryExpr e:
                return new UnaryExpr(e.Operator, Next(e.Value))
                {
                    IsRoot = e.IsRoot,
                };
            case RangeExpr e:
                var from = e.From == null
                    ? null
                    : Next(e.From);
                var to = e.To == null
                    ? null
                    : Next(e.To);

                return new RangeExpr(from, to, e.Inclusive)
                {
                    IsRoot = e.IsRoot,
                };
            case IndexerExpr e:
                return new IndexerExpr(Next(e.Value), Next(e.Index))
                {
                    IsRoot = e.IsRoot,
                };
            case VariableExpr e:
                var variableExpr = new VariableExpr(e.Identifier, e.ModuleName)
                {
                    IsRoot = e.IsRoot,
                };

                if (!e.Identifier.Value.StartsWith("$"))
                {
                    variableExpr.VariableSymbol = _scope.FindVariable(e.Identifier.Value);
                    if (variableExpr.VariableSymbol == null)
                        throw new RuntimeNotFoundException(e.Identifier.Value);
                }

                return variableExpr;
            case TypeExpr e:
                return Visit(e);
            case CallExpr e:
                return Visit(e);
            case FunctionReferenceExpr e:
                string name = e.Identifier.Value;
                string? moduleName = e.ModuleName?.Value;
                RuntimeFunction? runtimeFunction = null;

                // Try to resolve as std function
                var stdFunction = ResolveStdFunction(name, moduleName);
                if (stdFunction != null)
                    runtimeFunction = new RuntimeStdFunction(stdFunction);

                // Try to resolve as regular function
                if (runtimeFunction == null)
                {
                    var functionSymbol = ResolveFunctionSymbol(name, moduleName);
                    if (functionSymbol != null)
                        runtimeFunction = new RuntimeSymbolFunction(functionSymbol);
                }

                // Fallback: resolve as program
                runtimeFunction ??= new RuntimeProgramFunction(e.Identifier.Value);

                return new FunctionReferenceExpr(e.Identifier, e.ModuleName)
                {
                    RuntimeFunction = runtimeFunction,
                    IsRoot = e.IsRoot,
                };
            case ClosureExpr e:
                return new ClosureExpr(
                    Next(e.Function),
                    e.Parameters,
                    (BlockExpr)Next(e.Body)
                )
                {
                    IsRoot = e.IsRoot,
                };
        }

        return expr;
    }

    private LiteralExpr Visit(LiteralExpr expr)
    {
        IRuntimeValue value = expr.Value.Kind switch
        {
            TokenKind.IntegerLiteral => new RuntimeInteger(int.Parse(expr.Value.Value)),
            TokenKind.FloatLiteral => new RuntimeFloat(double.Parse(expr.Value.Value)),
            TokenKind.StringLiteral => new RuntimeString(expr.Value.Value),
            TokenKind.True => RuntimeBoolean.True,
            TokenKind.False => RuntimeBoolean.False,
            TokenKind.Nil => RuntimeNil.Value,
            _ => throw new ArgumentOutOfRangeException(),
        };

        var newExpr = new LiteralExpr(expr.Value)
        {
            RuntimeValue = value,
            IsRoot = expr.IsRoot,
        };

        return newExpr;
    }

    private TypeExpr Visit(TypeExpr expr)
    {
        if (!LanguageInfo.RuntimeTypes.TryGetValue(expr.Identifier.Value, out var type))
            throw new RuntimeNotFoundException(expr.Identifier.Value);

        var newExpr = new TypeExpr(expr.Identifier)
        {
            RuntimeValue = new RuntimeType(type),
            IsRoot = expr.IsRoot,
        };

        return newExpr;
    }

    private CallExpr Visit(CallExpr expr)
    {

        string name = expr.Identifier.Value;
        string? moduleName = expr.ModuleName?.Value;
        var stdFunction = ResolveStdFunction(name, moduleName);
        var functionSymbol = stdFunction == null
            ? ResolveFunctionSymbol(name, moduleName)
            : null;
        var callType = name switch
        {
            "cd" => CallType.BuiltInCd,
            "exec" => CallType.BuiltInExec,
            "scriptPath" => CallType.BuiltInScriptPath,
            "closure" => CallType.BuiltInClosure,
            "call" => CallType.BuiltInCall,
            _ => (stdFunction, functionSymbol) switch
            {
                (not null, null) => CallType.StdFunction,
                (null, not null) => CallType.Function,
                (null, null) => CallType.Program,
                _ => CallType.Function,
            },
        };

        var newExpr = new CallExpr(
            expr.Identifier,
            expr.Arguments.Select(Next).ToList(),
            expr.CallStyle,
            expr.Plurality,
            callType,
            expr.ModuleName
        )
        {
            IsRoot = expr.IsRoot,
            StdFunction = stdFunction,
            FunctionSymbol = functionSymbol,
        };

        return newExpr;
    }

    private MethodInfo? ResolveStdFunction(string name, string? moduleName)
    {
        bool isStdModule = moduleName != null && StdGateway.ContainsModule(moduleName);
        if (isStdModule || StdGateway.Contains(name))
        {
            if (isStdModule && !StdGateway.Contains(name, moduleName))
                throw new RuntimeNotFoundException(name);

            return StdGateway.GetFunction(name, moduleName);
        }

        return null;
    }

    private FunctionSymbol? ResolveFunctionSymbol(string name, string? moduleName)
    {
        var module = moduleName == null
            ? _scope.ModuleScope
            : _modules.Find(moduleName);
        if (module == null)
            throw new RuntimeModuleNotFoundException(moduleName!);

        return module.FindFunction(name);
    }
}
