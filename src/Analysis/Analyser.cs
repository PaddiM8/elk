using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Elk.Std.Bindings;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk.Analysis;

class Analyser
{
    private Scope _scope;
    private Expr? _lastExpr;

    private Analyser(RootModuleScope rootModule)
    {
        _scope = rootModule;
    }

    public static List<Expr> Analyse(IEnumerable<Expr> ast, ModuleScope module)
    {
        var analyser = new Analyser(module.RootModule);

        try
        {
            analyser.AnalyseModule(module);
            analyser._scope = module;

            return ast
                .Where(expr => expr is not FunctionExpr)
                .Select(expr => analyser.Next(expr))
                .ToList();
        }
        catch (RuntimeException e)
        {
            var error = new RuntimeError(
                e.Message,
                analyser._lastExpr?.Position ?? TextPos.Default
            );

            throw new AggregateException(error.ToString(), e)
            {
                Data =
                {
                    ["error"] = error,
                },
            };
        }
    }

    private void AnalyseModule(ModuleScope module)
    {
        module.IsAnalysed = true;

        foreach (var functionSymbol in module.Functions
                     .Concat(module.ImportedFunctions))
        {
            _scope = functionSymbol.Expr.Module;
            Next(functionSymbol.Expr);
        }

        foreach (var submodule in module.Modules
                     .Concat(module.ImportedModules)
                     .Where(x => !x.IsAnalysed))
            AnalyseModule(submodule);
    }

    private Expr Next(Expr expr)
    {
        _lastExpr = expr;

        switch (expr)
        {
            case ModuleExpr e:
                return new ModuleExpr(e.Identifier, (BlockExpr)Next(e.Body));
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
                    e.HasClosure
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
                var rightBinary = e.Operator == OperationKind.Pipe
                    ? NextCallOrClosure(e.Right, calledFromPipe: true, hasClosure: false)
                    : Next(e.Right);

                return new BinaryExpr(Next(e.Left), e.Operator, rightBinary)
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
                var variableExpr = new VariableExpr(e.Identifier)
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
                return Visit(e);
            case ClosureExpr e:
                return Visit(e);
        }

        return expr;
    }

    private Expr NextCallOrClosure(Expr expr, bool calledFromPipe, bool hasClosure)
    {
        _lastExpr = expr;

        return expr switch
        {
            ClosureExpr closureExpr => Visit(closureExpr, calledFromPipe),
            CallExpr callExpr => Visit(callExpr, calledFromPipe, hasClosure),
            _ => throw new RuntimeException("Expected a function call to the right of pipe."),
        };
    }

    private LiteralExpr Visit(LiteralExpr expr)
    {
        RuntimeObject value = expr.Value.Kind switch
        {
            TokenKind.IntegerLiteral => new RuntimeInteger(int.Parse(expr.Value.Value)),
            TokenKind.FloatLiteral => new RuntimeFloat(double.Parse(expr.Value.Value)),
            TokenKind.StringLiteral => new RuntimeString(expr.Value.Value),
            TokenKind.True => RuntimeBoolean.True,
            TokenKind.False => RuntimeBoolean.False,
            TokenKind.Nil => RuntimeNil.Value,
            TokenKind.RegexLiteral => new RuntimeRegex(new Regex(expr.Value.Value[1..^1])),
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
        var type = StdBindings.GetRuntimeType(expr.Identifier.Value);
        if (type == null)
            throw new RuntimeNotFoundException(expr.Identifier.Value);

        var newExpr = new TypeExpr(expr.Identifier)
        {
            RuntimeValue = new RuntimeType(type),
            IsRoot = expr.IsRoot,
        };

        return newExpr;
    }

    private CallExpr Visit(CallExpr expr, bool calledFromPipe = false, bool hasClosure = false)
    {
        string name = expr.Identifier.Value;
        CallType? builtIn = name switch
        {
            "cd" => CallType.BuiltInCd,
            "exec" => CallType.BuiltInExec,
            "scriptPath" => CallType.BuiltInScriptPath,
            "closure" => CallType.BuiltInClosure,
            "call" => CallType.BuiltInCall,
            "error" => CallType.BuiltInError,
            _ => null,
        };
        var stdFunction = !builtIn.HasValue
            ? ResolveStdFunction(name, expr.ModulePath)
            : null;
        var functionSymbol = stdFunction == null
            ? ResolveFunctionSymbol(name, expr.ModulePath)
            : null;
        var callType = builtIn switch
        {
            not null => builtIn.Value,
            _ => (stdFunction, functionSymbol) switch
            {
                (not null, null) => CallType.StdFunction,
                (null, not null) => CallType.Function,
                (null, null) => CallType.Program,
                _ => CallType.Function,
            },
        };

        int argumentCount = expr.Arguments.Count;
        if (calledFromPipe)
            argumentCount++;

        if (stdFunction != null)
        {
            if (argumentCount < stdFunction.MinArgumentCount ||
                argumentCount > stdFunction.MaxArgumentCount)
            {
                throw new RuntimeWrongNumberOfArgumentsException(
                    stdFunction.MinArgumentCount,
                    argumentCount,
                    stdFunction.VariadicStart.HasValue
                );
            }

            if (stdFunction.HasClosure && !hasClosure)
                throw new RuntimeException("Expected closure.");
        }

        var newExpr = new CallExpr(
            expr.Identifier,
            expr.ModulePath,
            expr.Arguments.Select(Next).ToList(),
            expr.CallStyle,
            expr.Plurality,
            callType
        )
        {
            IsRoot = expr.IsRoot,
            StdFunction = stdFunction,
            FunctionSymbol = functionSymbol,
        };

        return newExpr;
    }

    private Expr Visit(FunctionReferenceExpr expr)
    {
        string name = expr.Identifier.Value;
        RuntimeFunction? runtimeFunction = null;

        // Try to resolve as std function
        var stdFunction = ResolveStdFunction(name, expr.ModulePath);
        if (stdFunction != null)
            runtimeFunction = new RuntimeStdFunction(stdFunction);

        // Try to resolve as regular function
        if (runtimeFunction == null)
        {
            var functionSymbol = ResolveFunctionSymbol(name, expr.ModulePath);
            if (functionSymbol != null)
                runtimeFunction = new RuntimeSymbolFunction(functionSymbol);
        }

        // Fallback: resolve as program
        runtimeFunction ??= new RuntimeProgramFunction(expr.Identifier.Value);

        return new FunctionReferenceExpr(expr.Identifier, expr.ModulePath)
        {
            RuntimeFunction = runtimeFunction,
            IsRoot = expr.IsRoot,
        };
    }

    private Expr Visit(ClosureExpr e, bool calledFromPipe = false)
    {
        var function = e.Function is CallExpr callExpr
            ? NextCallOrClosure(callExpr, calledFromPipe, hasClosure: true)
            : Next(e.Function);

        return new ClosureExpr(
            function,
            e.Parameters,
            (BlockExpr)Next(e.Body)
        )
        {
            IsRoot = e.IsRoot,
        };
    }

    private StdFunction? ResolveStdFunction(string name, IList<Token> modulePath)
    {
        if (modulePath.Count > 1)
            return null;

        var moduleName = modulePath.FirstOrDefault()?.Value;
        var function = StdBindings.GetFunction(name, moduleName);
        if (function == null && moduleName != null && StdBindings.HasModule(moduleName))
                throw new RuntimeNotFoundException(name);

        return function;
    }

    private FunctionSymbol? ResolveFunctionSymbol(string name, IList<Token> modulePath)
    {
        var module = _scope.ModuleScope.FindModule(modulePath, lookInImports: true);
        if (module == null)
            throw new RuntimeModuleNotFoundException(modulePath);

        return module.FindFunction(name, lookInImports: true);
    }
}
