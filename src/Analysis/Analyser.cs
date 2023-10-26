using System;
using System.Collections.Generic;
using System.Linq;
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
    private Expr? _enclosingFunction;
    private Expr? _currentExpr;

    private Analyser(RootModuleScope rootModule)
    {
        _scope = rootModule;
    }

    public static IList<Expr> Analyse(IEnumerable<Expr> ast, ModuleScope module, bool isEntireModule)
    {
        if (isEntireModule && module.AnalysisStatus != AnalysisStatus.None)
            return module.Ast;

        var analyser = new Analyser(module.RootModule)
        {
            _scope = module,
        };

        module.AnalysisStatus = AnalysisStatus.Analysed;

        try
        {
            var analysedAst = ast
                .Select(expr => analyser.Next(expr))
                .ToList();

            if (isEntireModule)
                module.Ast = analysedAst;

            return analysedAst;
        }
        catch (RuntimeException ex)
        {
            ex.Position = analyser._currentExpr?.Position;
            throw;
        }
    }

    private Expr Next(Expr expr)
    {
        expr.EnclosingFunction = _enclosingFunction;
        _currentExpr = expr;

        var analysedExpr = expr switch
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
            SetExpr e => Visit(e),
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
            StringInterpolationExpr e => Visit(e),
            ClosureExpr e => Visit(e),
            TryExpr e => Visit(e),
            ThrowExpr e => Visit(e),
            _ => throw new NotSupportedException(),
        };

        analysedExpr.EnclosingFunction = expr.EnclosingFunction;

        return analysedExpr;
    }

    private Expr NextCallOrClosure(Expr expr, Expr? pipedValue, bool hasClosure, bool validateParameters = true)
    {
        expr.EnclosingFunction = _enclosingFunction;
        _currentExpr = expr;

        var analysedExpr = expr switch
        {
            ClosureExpr closureExpr => Visit(closureExpr, pipedValue),
            CallExpr callExpr => Visit(callExpr, pipedValue, hasClosure, validateParameters),
            _ => throw new RuntimeException("Expected a function call to the right of pipe."),
        };

        analysedExpr.EnclosingFunction = expr.EnclosingFunction;

        return analysedExpr;
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
        if (expr.AnalysisStatus != AnalysisStatus.None)
            return expr;

        expr.EnclosingFunction = expr;
        var parameters = AnalyseParameters(expr.Parameters);
        foreach (var parameter in parameters)
        {
            expr.Block.Scope.AddVariable(parameter.Identifier.Value, RuntimeNil.Value);
        }

        var newFunction = new FunctionExpr(
            expr.Identifier,
            parameters,
            expr.Block,
            expr.Module,
            expr.HasClosure
        )
        {
            IsRoot = expr.IsRoot,
            AnalysisStatus = AnalysisStatus.Analysed,
        };

        // Need to set _enclosingFunction *before* analysing the block
        // since it's used inside the block.
        _enclosingFunction = newFunction;

        try
        {
            var previousScope = _scope;
            _scope = expr.Module;
            newFunction.Block = (BlockExpr)Next(expr.Block);
            _scope = previousScope;
        }
        catch (RuntimeException)
        {
            expr.AnalysisStatus = AnalysisStatus.Failed;

            throw;
        }

        _enclosingFunction = null;
        expr.Module.AddFunction(newFunction);

        return newFunction;
    }

    private List<Parameter> AnalyseParameters(ICollection<Parameter> parameters)
    {
        var encounteredDefaultParameter = false;
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
        {
            var firstModule = expr.ModulePath.FirstOrDefault()?.Value;
            if (firstModule == null || !StdBindings.HasModule(firstModule))
                throw new RuntimeModuleNotFoundException(expr.ModulePath);

            var stdStruct = StdBindings.GetStruct(expr.Identifier.Value, firstModule);
            if (stdStruct == null)
                throw new RuntimeNotFoundException(expr.Identifier.Value);

            var argumentCount = expr.Arguments.Count;
            if (argumentCount < stdStruct.MinArgumentCount ||
                argumentCount > stdStruct.MaxArgumentCount)
            {
                throw new RuntimeWrongNumberOfArgumentsException(
                    stdStruct.MinArgumentCount,
                    argumentCount,
                    stdStruct.VariadicStart.HasValue
                );
            }

            return new NewExpr(
                expr.Identifier,
                expr.ModulePath,
                expr.Arguments.Select(Next).ToList()
            )
            {
                StructSymbol = new StructSymbol(stdStruct),
            };
        }

        var symbol = module.FindStruct(expr.Identifier.Value, lookInImports: true);
        if (symbol?.Expr == null)
            throw new RuntimeNotFoundException(expr.Identifier.Value);

        ValidateArguments(
            expr.Arguments,
            symbol.Expr.Parameters,
            isReference: false
        );

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

    private SetExpr Visit(SetExpr expr)
    {
        foreach (var value in expr.Entries)
            Next(value);

        return new SetExpr(expr.Entries.Select(Next).ToList(), expr.Position)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private DictionaryExpr Visit(DictionaryExpr expr)
    {
        var dictEntries = expr.Entries
            .Select(x => (Next(x.Item1), Next(x.Item2)))
            .ToList();

        return new DictionaryExpr(dictEntries, expr.Position)
        {
            IsRoot = expr.IsRoot,
        };
    }

    private Expr Visit(BlockExpr expr)
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
        if (expr.Operator == OperationKind.Equals)
            return AnalyseAssignment(expr);

        if (expr.Operator is OperationKind.NonRedirectingAnd or OperationKind.NonRedirectingOr)
        {
            expr.Left.IsRoot = true;

            // Necessary for nested non-redirecting and/or expressions
            if (expr.Left is BinaryExpr
                    { Operator: OperationKind.NonRedirectingAnd or OperationKind.NonRedirectingOr } binaryRight)
            {
                binaryRight.Right.IsRoot = true;
            }
        }


        var left = Next(expr.Left);
        if (expr.Operator is OperationKind.Pipe or OperationKind.PipeErr or OperationKind.PipeAll)
        {
            expr.Right.IsRoot = expr.IsRoot;

            var isProgramCall = left is CallExpr { CallType: CallType.Program };
            if (!isProgramCall && expr.Operator is OperationKind.PipeErr or OperationKind.PipeAll)
            {
                var pipeString = expr.Operator == OperationKind.PipeErr ? "|err" : "|all";

                throw new RuntimeInvalidOperationException(pipeString, "non-program");
            }

            if (isProgramCall)
            {
                var leftCall = (CallExpr)left;
                leftCall.RedirectionKind = expr.Operator switch
                {
                    OperationKind.PipeErr => RedirectionKind.Error,
                    OperationKind.PipeAll => RedirectionKind.All,
                    _ => RedirectionKind.Output,
                };
            }

            return NextCallOrClosure(expr.Right, left, hasClosure: false);
        }

        return new BinaryExpr(left, expr.Operator, Next(expr.Right))
        {
            IsRoot = expr.IsRoot,
        };
    }

    private Expr AnalyseAssignment(BinaryExpr expr)
    {
        if (expr.Left is VariableExpr variableExpr)
        {
            if (!_scope.HasVariable(variableExpr.Identifier.Value))
                throw new RuntimeNotFoundException(variableExpr.Identifier.Value);
        }
        else if (expr.Left is not (IndexerExpr or FieldAccessExpr))
        {
            throw new RuntimeException("Invalid assignment");
        }

        return new BinaryExpr(Next(expr.Left), expr.Operator, Next(expr.Right))
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
        return new FieldAccessExpr(Next(expr.Object), expr.Identifier)
        {
            IsRoot = expr.IsRoot,
            RuntimeIdentifier = new RuntimeString(expr.Identifier.Value),
        };
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
            if (!_scope.HasVariable(expr.Identifier.Value))
                throw new RuntimeNotFoundException(expr.Identifier.Value);
        }

        if (expr.EnclosingFunction is ClosureExpr closure &&
            closure.Body.Scope.Parent?.HasVariable(expr.Identifier.Value) is true)
        {
            closure.CapturedVariables.Add(expr.Identifier.Value);
        }

        return variableExpr;
    }

    private CallExpr Visit(
        CallExpr expr,
        Expr? pipedValue = null,
        bool hasClosure = false,
        bool validateParameters = true)
    {
        var name = expr.Identifier.Value;
        CallType? builtIn = name switch
        {
            "cd" => CallType.BuiltInCd,
            "exec" => CallType.BuiltInExec,
            "scriptPath" => CallType.BuiltInScriptPath,
            "closure" => CallType.BuiltInClosure,
            "call" => CallType.BuiltInCall,
            "time" => CallType.BuiltInTime,
            _ => null,
        };

        if (builtIn == CallType.BuiltInClosure && expr.EnclosingFunction is not FunctionExpr { HasClosure: true })
        {
            throw new RuntimeException(
                "Unexpected call to 'closure'. This function can only be called within functions with a closure signature."
            );
        }

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

        var evaluatedArguments = expr.Arguments.Select(Next).ToList();
        if (pipedValue != null && callType != CallType.Program)
            evaluatedArguments.Insert(0, pipedValue);

        var definitionHasClosure = functionSymbol?.Expr.HasClosure is true || stdFunction?.HasClosure is true;
        if (!definitionHasClosure && hasClosure)
        {
            var additionalInfo = callType == CallType.Program
                ? " The call was evaluated as a program invocation since a function with this name could not be found."
                : "";

            throw new RuntimeException($"Unexpected closure.{additionalInfo}");
        }

        if (definitionHasClosure && !hasClosure)
            throw new RuntimeException("Expected closure.");

        var argumentCount = evaluatedArguments.Count;
        if (stdFunction != null && validateParameters)
        {
            var hasEnoughArguments = expr.IsReference || argumentCount >= stdFunction.MinArgumentCount;
            if (!hasEnoughArguments || argumentCount > stdFunction.MaxArgumentCount)
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

        if (functionSymbol != null && validateParameters)
        {
            if (hasClosure && !functionSymbol.Expr.HasClosure)
                throw new RuntimeException("Expected closure.");

            ValidateArguments(
                evaluatedArguments,
                functionSymbol.Expr.Parameters,
                expr.IsReference
            );
        }

        if (pipedValue is CallExpr { CallType: CallType.Program } pipedCall)
        {
            // Don't buffer stdout/stderr if it's piped straight to a program's stdin
            // or piped to an std function that expects a Pipe. Std functions that
            // explicitly expect Pipes will handle them properly and not pass them
            // around more.
            pipedCall.DisableRedirectionBuffering = callType == CallType.Program ||
                stdFunction?.ConsumesPipe is true;
        }

        if (stdFunction?.StartsPipeManually is true)
        {
            foreach (var argument in evaluatedArguments)
            {
                if (argument is CallExpr call)
                    call.AutomaticStart = false;
            }
        }

        var environmentVariables = new Dictionary<string, Expr>();
        foreach (var (key, value) in expr.EnvironmentVariables)
        {
            environmentVariables.Add(key, Next(value));
        }

        return new CallExpr(
            expr.Identifier,
            expr.ModulePath,
            evaluatedArguments,
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
            RedirectionKind = expr.RedirectionKind,
            DisableRedirectionBuffering = expr.DisableRedirectionBuffering,
            IsReference = expr.IsReference,
            EnvironmentVariables = environmentVariables,
        };
    }

    private void ValidateArguments(IList<Expr> arguments, IList<Parameter> parameters, bool isReference)
    {
        var argumentCount = arguments.Count;
        var isVariadic = parameters.LastOrDefault()?.IsVariadic is true;
        var tooManyArguments = argumentCount > parameters.Count && !isVariadic;
        var tooFewArguments = !isReference && !isVariadic && parameters.Count > argumentCount &&
            parameters[argumentCount].DefaultValue == null;

        if (tooManyArguments || tooFewArguments)
            throw new RuntimeWrongNumberOfArgumentsException(parameters.Count, argumentCount, isVariadic);

        if (parameters.LastOrDefault()?.IsVariadic is true)
        {
            var variadicStart = parameters.Count - 1;
            var variadicArguments = new Expr[arguments.Count - variadicStart];
            for (var i = 0; i < variadicArguments.Length; i++)
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
            TokenKind.IntegerLiteral => new RuntimeInteger(ParseInt(expr.Value.Value)),
            TokenKind.FloatLiteral => new RuntimeFloat(double.Parse(expr.Value.Value)),
            TokenKind.StringLiteral => new RuntimeString(expr.Value.Value),
            TokenKind.True => RuntimeBoolean.True,
            TokenKind.False => RuntimeBoolean.False,
            _ => RuntimeNil.Value,
        };

        var newExpr = new LiteralExpr(expr.Value)
        {
            RuntimeValue = value,
            IsRoot = expr.IsRoot,
        };

        return newExpr;
    }

    private int ParseInt(string numberLiteral)
    {
        try
        {
            if (numberLiteral.StartsWith("0x"))
                return Convert.ToInt32(numberLiteral[2..], 16);
            if (numberLiteral.StartsWith("0o"))
                return Convert.ToInt32(numberLiteral[2..], 8);
            if (numberLiteral.StartsWith("0b"))
                return Convert.ToInt32(numberLiteral[2..], 2);
        }
        catch
        {
            throw new RuntimeException("Invalid number literal");
        }

        return int.Parse(numberLiteral);
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
        expr.EnclosingFunction = expr;

        var function = (CallExpr)NextCallOrClosure(expr.Function, pipedValue, hasClosure: true);
        var closure = new ClosureExpr(function, expr.Parameters, expr.Body)
        {
            IsRoot = expr.IsRoot,
            CapturedVariables = expr.CapturedVariables,
        };

        var previousEnclosingFunction = _enclosingFunction;
        _enclosingFunction = closure;
        closure.Body = (BlockExpr)Next(expr.Body);
        _enclosingFunction = previousEnclosingFunction;

        if (closure.Body.Expressions.Count != 1 ||
            closure.Body.Expressions.First() is not CallExpr { IsReference: true } functionReference)
        {
            return closure;
        }

        var closureParameters = function switch
        {
            not { StdFunction: null } => Enumerable
                .Repeat(functionReference.Identifier, function.StdFunction.ClosureParameterCount!.Value)
                .Select((x, i) => x with { Value = "'" + i }),
            not { FunctionSymbol: null } => function.FunctionSymbol.Expr.Parameters
                .Select(x => x.Identifier with { Value = "'" + x.Identifier.Value }),
            _ => new List<Token> { functionReference.Identifier with { Value = "'a" } },
        };
        var implicitArguments = closureParameters
            .Select(x => new VariableExpr(x)
            {
                EnclosingFunction = closure,
            });
        functionReference.Arguments = implicitArguments
            .Concat(functionReference.Arguments)
            .ToList();
        functionReference.IsReference = false;

        foreach (var parameter in closureParameters)
            closure.Body.Scope.AddVariable(parameter.Value, RuntimeNil.Value);

        closure.Parameters.Clear();
        closure.Parameters.AddRange(closureParameters);
        closure.Body.Expressions.Clear();
        closure.Body.Expressions.Add(functionReference);

        return closure;
    }

    private TryExpr Visit(TryExpr expr)
    {
        return new TryExpr(
            (BlockExpr)Next(expr.Body),
            (BlockExpr)Next(expr.CatchBody),
            expr.CatchIdentifier
        );
    }

    private ThrowExpr Visit(ThrowExpr expr)
        => new(Next(expr.Value));

    private StdFunction? ResolveStdFunction(string name, IList<Token> modulePath)
    {
        var module = modulePath.Select(x => x.Value);
        var function = StdBindings.GetFunction(name, module);
        if (function == null && StdBindings.HasModule(module))
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