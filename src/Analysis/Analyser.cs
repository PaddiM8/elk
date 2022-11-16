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
            StructExpr e => Visit(e),
            FunctionExpr e => Visit(e),
            LetExpr e => Visit(e),
            NewExpr e => Visit(e),
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
            FieldAccessExpr e => Visit(e),
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

    private Expr NextCallOrClosure(Expr expr, Expr? pipedValue, bool hasClosure)
    {
        _lastExpr = expr;

        return expr switch
        {
            ClosureExpr closureExpr => Visit(closureExpr, pipedValue),
            CallExpr callExpr => Visit(callExpr, pipedValue, hasClosure),
            _ => throw new RuntimeException("Expected a function call to the right of pipe."),
        };
    }

    private ModuleExpr Visit(ModuleExpr expr)
    {
        var block = (BlockExpr)Next(expr.Body);
        block.IsRoot = true;

        return new ModuleExpr(expr.Identifier, block);
    }

    private StructExpr Visit(StructExpr expr)
    {
        var newStruct = new StructExpr(
            expr.Identifier,
            AnalyseParameters(expr.Parameters),
            expr.Module
        );

        var uniqueParameters = new HashSet<string>(newStruct.Parameters.Select(x => x.Identifier.Value));
        if (uniqueParameters.Count != newStruct.Parameters.Count)
            throw new RuntimeException("Duplicate field in struct");

        expr.Module.AddStruct(newStruct);

        return newStruct;
    }

    private FunctionExpr Visit(FunctionExpr expr)
    {
        var parameters = AnalyseParameters(expr.Parameters);
        foreach (var parameter in parameters)
        {
            expr.Block.Scope.AddVariable(parameter.Identifier.Value, RuntimeNil.Value);
        }

        var newFunction = new FunctionExpr(
            expr.Identifier,
            parameters,
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

    private List<Parameter> AnalyseParameters(ICollection<Parameter> parameters)
    {
        bool encounteredDefaultParameter = false;
        var newParameters = new List<Parameter>();
        foreach (var (parameter, i) in parameters.WithIndex())
        {
            if (parameter.DefaultValue == null)
            {
                if (encounteredDefaultParameter)
                    throw new RuntimeException("Optional parameters may only occur at the end of parameter lists");

                newParameters.Add(parameter);
            }
            else
            {
                newParameters.Add(
                    parameter with { DefaultValue = Next(parameter.DefaultValue) }
                );
                encounteredDefaultParameter = true;
            }

            if (parameter.IsVariadic)
            {
                if (i != parameters.Count - 1)
                    throw new RuntimeException("Variadic parameters may only occur at the end of parameter lists");

                break;
            }
        }

        return newParameters;
    }

    private LetExpr Visit(LetExpr expr)
    {
        return new LetExpr(expr.IdentifierList, Next(expr.Value));
    }

    private NewExpr Visit(NewExpr expr)
    {
        var module = _scope.ModuleScope.FindModule(expr.ModulePath, lookInImports: true);
        if (module == null)
            throw new RuntimeModuleNotFoundException(expr.ModulePath);

        var symbol = module.FindStruct(expr.Identifier.Value, lookInImports: true);
        if (symbol == null)
            throw new RuntimeNotFoundException(expr.Identifier.Value);

        ValidateArguments(expr.Arguments, symbol.Expr.Parameters);

        return new NewExpr(
            expr.Identifier,
            expr.ModulePath,
            expr.Arguments.Select(Next).ToList()
        )
        {
            StructSymbol = symbol,
        };
    }

    private IfExpr Visit(IfExpr expr)
    {
        var ifCondition = Next(expr.Condition);
        var thenBranch = Next(expr.ThenBranch);
        thenBranch.IsRoot = expr.IsRoot;

        Expr? elseBranch = null;
        if (expr.ElseBranch != null)
        {
            elseBranch = Next(expr.ElseBranch);
            elseBranch.IsRoot = expr.IsRoot;
        }

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
        branch.IsRoot = true;

        return new ForExpr(expr.IdentifierList, forValue, branch)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private WhileExpr Visit(WhileExpr expr)
    {
        var whileCondition = Next(expr.Condition);
        var whileBranch = (BlockExpr)Next(expr.Branch);
        whileBranch.IsRoot = true;

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
        var blockExpressions = new List<Expr>();
        foreach (var analysed in expr.Expressions.Select(Next))
        {
            // The "IsRoot" value of the last expression
            // is decided on the fly, in the interpreter.
            analysed.IsRoot = true;
            blockExpressions.Add(analysed);
        }

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

    private Expr Visit(BinaryExpr expr)
    {
        var left = Next(expr.Left);
        if (expr.Operator == OperationKind.Pipe)
            return NextCallOrClosure(expr.Right, left, hasClosure: false);

        return new BinaryExpr(left, expr.Operator, Next(expr.Right))
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

    private FieldAccessExpr Visit(FieldAccessExpr expr)
    {
        return new FieldAccessExpr(Next(expr.Object), expr.Identifier);
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

    private IndexerExpr Visit(IndexerExpr expr)
    {
        return new IndexerExpr(Next(expr.Value), Next(expr.Index))
        {
            IsRoot = expr.IsRoot,
        };
    }

    private TypeExpr Visit(TypeExpr expr)
    {
        var stdType = StdBindings.GetRuntimeType(expr.Identifier.Value);
        var userType = _scope.ModuleScope.FindStruct(expr.Identifier.Value, lookInImports: true);
        if (stdType == null && userType == null)
            throw new RuntimeNotFoundException(expr.Identifier.Value);

        var newExpr = new TypeExpr(expr.Identifier)
        {
            RuntimeValue = stdType != null
                ? new RuntimeType(stdType)
                : new RuntimeType(userType!),
            IsRoot = expr.IsRoot,
        };

        return newExpr;
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

    private CallExpr Visit(CallExpr expr, Expr? pipedValue = null, bool hasClosure = false)
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

        if (pipedValue != null && callType != CallType.Program)
            expr.Arguments.Insert(0, pipedValue);

        if (callType != CallType.StdFunction && callType != CallType.Function && hasClosure)
            throw new RuntimeException("Unexpected closure");

        int argumentCount = expr.Arguments.Count;
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

        if (functionSymbol != null)
        {
            if (hasClosure && !functionSymbol.Expr.HasClosure)
                throw new RuntimeException("Expected closure.");

            ValidateArguments(expr.Arguments, functionSymbol.Expr.Parameters);
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
            PipedToProgram = callType == CallType.Program
                ? pipedValue
                : null,
        };

        return newExpr;
    }

    private void ValidateArguments(IList<Expr> arguments, IList<Parameter> parameters)
    {
        int argumentCount = arguments.Count;
        bool isVariadic = parameters.LastOrDefault()?.IsVariadic is true;
        bool tooManyArguments = argumentCount > parameters.Count && !isVariadic;
        bool tooFewArguments = parameters.Count > argumentCount &&
           parameters[argumentCount].DefaultValue == null && !isVariadic;

        if (tooManyArguments || tooFewArguments)
            throw new RuntimeWrongNumberOfArgumentsException(parameters.Count, argumentCount, isVariadic);

        if (parameters.LastOrDefault()?.IsVariadic is true)
        {
            int variadicStart = parameters.Count - 1;
            var variadicArguments = new Expr[arguments.Count - variadicStart];
            for (int i = 0; i < variadicArguments.Length; i++)
            {
                variadicArguments[^(i + 1)] = arguments.Last();
                arguments.RemoveAt(arguments.Count - 1);
            }

            arguments.Add(
                new ListExpr(variadicArguments, variadicArguments.FirstOrDefault()?.Position ?? TextPos.Default)
            );
        }
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

    private StringInterpolationExpr Visit(StringInterpolationExpr expr)
    {
        var parts = expr.Parts.Select(Next).ToList();

        return new StringInterpolationExpr(parts, expr.Position)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private Expr Visit(ClosureExpr expr, Expr? pipedValue = null)
    {
        var function = expr.Function is CallExpr callExpr
            ? NextCallOrClosure(callExpr, pipedValue, hasClosure: true)
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