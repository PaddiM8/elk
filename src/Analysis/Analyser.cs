using System;
using System.Collections.Generic;
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

        return expr switch
        {
            ModuleExpr e => Visit(e),
            FunctionExpr e => Visit(e),
            LetExpr e => Visit(e),
            IfExpr e => Visit(e),
            ForExpr e => Visit(e),
            WhileExpr e => Visit(e),
            TupleExpr e => Visit(e),
            ListExpr e => Visit(e),
            DictionaryExpr e => Visit(e),
            BlockExpr e => Visit(e),
            KeywordExpr e => Visit(e),
            BinaryExpr e => Visit(e),
            UnaryExpr e => Visit(e),
            RangeExpr e => Visit(e),
            IndexerExpr e => Visit(e),
            TypeExpr e => Visit(e),
            VariableExpr e => Visit(e),
            CallExpr e => Visit(e),
            LiteralExpr e => Visit(e),
            FunctionReferenceExpr e => Visit(e),
            StringInterpolationExpr e => Visit(e),
            ClosureExpr e => Visit(e),
            _ => throw new NotSupportedException(),
        };
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

    private ModuleExpr Visit(ModuleExpr expr)
    {
        return new ModuleExpr(expr.Identifier, (BlockExpr)Next(expr.Body));
    }

    private FunctionExpr Visit(FunctionExpr expr)
    {
        foreach (var parameter in expr.Parameters)
        {
            if (parameter.DefaultValue != null)
                Next(parameter.DefaultValue!);

            expr.Block.Scope.AddVariable(parameter.Identifier.Value, RuntimeNil.Value);
        }

        var newFunction = new FunctionExpr(
            expr.Identifier,
            expr.Parameters,
            (BlockExpr)Next(expr.Block),
            expr.Module,
            expr.HasClosure
        )
        {
            IsRoot = expr.IsRoot,
        };

        expr.Module.AddFunction(newFunction);

        return newFunction;
    }

    private LetExpr Visit(LetExpr expr)
    {
        return new LetExpr(expr.IdentifierList, Next(expr.Value));
    }

    private KeywordExpr Visit(KeywordExpr expr)
    {
        Expr? keywordValue = null;
        if (expr.Value != null)
            keywordValue = Next(expr.Value);

        return new KeywordExpr(expr.Kind, keywordValue, expr.Position)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private IfExpr Visit(IfExpr expr)
    {
        var ifCondition = Next(expr.Condition);
        var thenBranch = Next(expr.ThenBranch);
        Expr? elseBranch = null;
        if (expr.ElseBranch != null)
            elseBranch = Next(expr.ElseBranch);

        return new IfExpr(ifCondition, thenBranch, elseBranch)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private ForExpr Visit(ForExpr expr)
    {
        var forValue = Next(expr.Value);
        foreach (var identifier in expr.IdentifierList)
            expr.Branch.Scope.AddVariable(identifier.Value, RuntimeNil.Value);

        var branch = (BlockExpr)Next(expr.Branch);

        return new ForExpr(expr.IdentifierList, forValue, branch)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private WhileExpr Visit(WhileExpr expr)
    {
        var whileCondition = Next(expr.Condition);
        var whileBranch = (BlockExpr)Next(expr.Branch);

        return new WhileExpr(whileCondition, whileBranch)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private TupleExpr Visit(TupleExpr expr)
    {
        var tupleValues = expr.Values.Select(Next).ToList();

        return new TupleExpr(tupleValues, expr.Position)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private ListExpr Visit(ListExpr expr)
    {
        var listValues = expr.Values.Select(Next).ToList();

        return new ListExpr(listValues, expr.Position)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private DictionaryExpr Visit(DictionaryExpr expr)
    {
        foreach (var (_, value) in expr.Entries)
            Next(value);
        var dictEntries = expr.Entries
            .Select(x => (x.Item1, Next(x.Item2)))
            .ToList();

        return new DictionaryExpr(dictEntries, expr.Position)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private BlockExpr Visit(BlockExpr expr)
    {
        _scope = expr.Scope;
        var blockExpressions = expr.Expressions.Select(Next).ToList();
        var newExpr = new BlockExpr(
            blockExpressions,
            expr.ParentStructureKind,
            expr.Position,
            expr.Scope
        )
        {
            IsRoot = expr.IsRoot,
        };
        _scope = _scope.Parent!;

        return newExpr;
    }

    private RangeExpr Visit(RangeExpr expr)
    {
        var from = expr.From == null
            ? null
            : Next(expr.From);
        var to = expr.To == null
            ? null
            : Next(expr.To);

        return new RangeExpr(from, to, expr.Inclusive)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private VariableExpr Visit(VariableExpr expr)
    {
        var variableExpr = new VariableExpr(expr.Identifier)
        {
            IsRoot = expr.IsRoot,
        };

        if (!expr.Identifier.Value.StartsWith("$"))
        {
            variableExpr.VariableSymbol = _scope.FindVariable(expr.Identifier.Value);
            if (variableExpr.VariableSymbol == null)
                throw new RuntimeNotFoundException(expr.Identifier.Value);
        }

        return variableExpr;
    }

    private IndexerExpr Visit(IndexerExpr expr)
    {
        return new IndexerExpr(Next(expr.Value), Next(expr.Index))
        {
            IsRoot = expr.IsRoot,
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

    private StringInterpolationExpr Visit(StringInterpolationExpr expr)
    {
        var parts = expr.Parts.Select(Next).ToList();

        return new StringInterpolationExpr(parts, expr.Position)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private BinaryExpr Visit(BinaryExpr expr)
    {
        var rightBinary = expr.Operator == OperationKind.Pipe
            ? NextCallOrClosure(expr.Right, calledFromPipe: true, hasClosure: false)
            : Next(expr.Right);

        return new BinaryExpr(Next(expr.Left), expr.Operator, rightBinary)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private UnaryExpr Visit(UnaryExpr expr)
    {
        return new UnaryExpr(expr.Operator, Next(expr.Value))
        {
            IsRoot = expr.IsRoot,
        };
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

    private Expr Visit(ClosureExpr expr, bool calledFromPipe = false)
    {
        var function = expr.Function is CallExpr callExpr
            ? NextCallOrClosure(callExpr, calledFromPipe, hasClosure: true)
            : Next(expr.Function);

        return new ClosureExpr(
            function,
            expr.Parameters,
            (BlockExpr)Next(expr.Body)
        )
        {
            IsRoot = expr.IsRoot,
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