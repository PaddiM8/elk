using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Highlighting;
using Elk.Std.Bindings;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.DataTypes;

namespace Elk.Analysis;

class Analyser(RootModuleScope rootModule)
{
    private Scope _scope = rootModule;
    private Expr? _enclosingFunction;
    private Expr? _currentExpr;
    private List<SemanticToken>? _semanticTokens;

    public static IList<SemanticToken> GetSemanticTokens(IEnumerable<Expr> ast, ModuleScope module)
    {
        var analyser = new Analyser(module.RootModule)
        {
            _scope = module,
            _semanticTokens = [],
        };
        analyser.Start(ast, module, AnalysisScope.OverwriteExistingModule);

        return analyser._semanticTokens;
    }

    public static IList<Expr> Analyse(IEnumerable<Expr> ast, ModuleScope module, AnalysisScope analysisScope)
    {
        if (analysisScope == AnalysisScope.OncePerModule && module.AnalysisStatus != AnalysisStatus.None)
            return module.Ast;

        var analyser = new Analyser(module.RootModule)
        {
            _scope = module,
        };

        return analyser.Start(ast, module, analysisScope);
    }

    private IList<Expr> Start(IEnumerable<Expr> ast, ModuleScope module, AnalysisScope analysisScope)
    {
        module.AnalysisStatus = AnalysisStatus.Analysed;
        ResolveImports(module);

        try
        {
            var analysedAst = ast
                .Select(expr => Next(expr))
                .ToList();

            if (analysisScope == AnalysisScope.OncePerModule)
                module.Ast = analysedAst;

            return analysedAst;
        }
        catch (RuntimeException ex)
        {
            ex.Position = _currentExpr?.Position;
            throw;
        }
    }

    private static void ResolveImports(ModuleScope module)
    {
        foreach (var (importScope, token) in module.ImportedUnknowns)
        {
            var importedFunction = importScope.FindFunction(token.Value, lookInImports: false);
            if (importedFunction != null)
            {
                if (importedFunction.Expr.AccessLevel != AccessLevel.Public)
                {
                    throw new RuntimeException(
                        $"Cannot import private symbol '{importedFunction.Expr.Identifier.Value}'",
                        token.Position
                    );
                }

                module.ImportFunction(importedFunction);
                continue;
            }

            var importedStruct = importScope.FindStruct(token.Value, lookInImports: false);
            if (importedStruct != null)
            {
                if (importedStruct.Expr?.AccessLevel is not (AccessLevel.Public or null))
                {
                    throw new RuntimeException(
                        $"Cannot import private symbol '{importedStruct.Expr?.Identifier.Value}'",
                        token.Position
                    );
                }

                module.ImportStruct(importedStruct);
                continue;
            }

            var importedModule = importScope.FindModule([token], lookInImports: false);
            if (importedModule != null)
            {
                if (importedModule.AccessLevel != AccessLevel.Public)
                {
                    throw new RuntimeException(
                        $"Cannot import private symbol '{importedModule.Name}'",
                        token.Position
                    );
                }

                module.ImportModule(token.Value, importedModule);
                continue;
            }

            if (importedModule == null)
            {
                throw new RuntimeException(
                    $"Module does not contain symbol '{token.Value}'",
                    token.Position
                );
            }
        }

        module.ClearUnknowns();
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
            _ => throw new RuntimeException("Expected function call to the right of pipe."),
        };

        analysedExpr.EnclosingFunction = expr.EnclosingFunction;

        return analysedExpr;
    }

    private ModuleExpr Visit(ModuleExpr expr)
    {
        AddSemanticToken(SemanticTokenKind.Module, expr.Identifier);

        var block = (BlockExpr)Next(expr.Body);
        block.IsRoot = true;

        return new ModuleExpr(expr.AccessLevel, expr.Identifier, block);
    }

    private StructExpr Visit(StructExpr expr)
    {
        AddSemanticToken(SemanticTokenKind.Struct, expr.Identifier);

        var newStruct = new StructExpr(
            expr.AccessLevel,
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

        AddSemanticToken(SemanticTokenKind.Function, expr.Identifier);

        expr.EnclosingFunction = expr;
        var parameters = AnalyseParameters(expr.Parameters);
        foreach (var parameter in parameters)
        {
            expr.Block.Scope.AddVariable(parameter.Identifier.Value, RuntimeNil.Value);
        }

        var newFunction = new FunctionExpr(
            expr.AccessLevel,
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
            AddSemanticToken(SemanticTokenKind.Parameter, parameter.Identifier);

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
        AddSemanticTokens(SemanticTokenKind.Variable, expr.IdentifierList);

        return new LetExpr(expr.IdentifierList, Next(expr.Value));
    }

    private NewExpr Visit(NewExpr expr)
    {
        AddSemanticTokens(SemanticTokenKind.Module, expr.ModulePath);

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

            AddSemanticToken(SemanticTokenKind.Struct, expr.Identifier);

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

        if (module != _scope.ModuleScope && symbol.Expr.AccessLevel != AccessLevel.Public)
            throw new RuntimeAccessLevelException(symbol.Expr.AccessLevel, expr.Identifier.Value);

        AddSemanticToken(SemanticTokenKind.Struct, expr.Identifier);
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
        AddSemanticTokens(SemanticTokenKind.Variable, expr.IdentifierList);

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

        if (!expr.Identifier.Value.StartsWith('$'))
        {
            if (!_scope.HasVariable(expr.Identifier.Value))
                throw new RuntimeNotFoundException(expr.Identifier.Value);
        }

        if (expr.EnclosingFunction is ClosureExpr closure &&
            closure.Body.Scope.Parent?.HasVariable(expr.Identifier.Value) is true)
        {
            closure.CapturedVariables.Add(expr.Identifier.Value);
        }

        AddSemanticToken(SemanticTokenKind.Variable, expr.Identifier);

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

        foreach (var moduleIdentifier in expr.ModulePath)
            AddSemanticToken(SemanticTokenKind.Module, moduleIdentifier);

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

        if (_semanticTokens != null && expr.CallStyle == CallStyle.TextArguments)
        {
            AddSemanticToken(SemanticTokenKind.Function, expr.Identifier, SemanticFeature.TextArgumentCall);
            foreach (var argument in expr.Arguments)
            {
                if (argument is LiteralExpr literalExpr)
                    AddSemanticToken(SemanticTokenKind.String, literalExpr.Value);

                if (argument is not StringInterpolationExpr interpolationExpr)
                    continue;

                foreach (var part in interpolationExpr.Parts)
                {
                    if (part is LiteralExpr literalPart)
                        AddSemanticToken(SemanticTokenKind.String, literalPart.Value);
                }
            }
        }
        else
        {
            AddSemanticToken(SemanticTokenKind.Function, expr.Identifier);
        }

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
            TokenKind.DoubleQuoteStringLiteral or TokenKind.SingleQuoteStringLiteral => new RuntimeString(expr.Value.Value),
            TokenKind.TextArgumentStringLiteral => new RuntimeString(expr.Value.Value)
            {
                IsTextArgument = true,
            },
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

    private static long ParseInt(string numberLiteral)
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

        return long.Parse(numberLiteral);
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

        AddSemanticTokens(SemanticTokenKind.Parameter, expr.Parameters);

        var previousEnclosingFunction = _enclosingFunction;
        _enclosingFunction = closure;
        closure.Body = (BlockExpr)Next(expr.Body);
        _enclosingFunction = previousEnclosingFunction;

        // If closure inside a closure captures a variable that is outside its parent,
        // the parent needs to capture it as well, in order to pass it on to the child.
        if (_enclosingFunction is ClosureExpr enclosingClosure)
        {
            foreach (var captured in expr.CapturedVariables
                .Where(x => enclosingClosure.Body.Scope.HasVariable(x)))
            {
                enclosingClosure.CapturedVariables.Add(captured);
            }
        }

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
        var tryBranch = (BlockExpr)Next(expr.Body);
        if (expr.CatchIdentifier != null)
            AddSemanticToken(SemanticTokenKind.Parameter, expr.CatchIdentifier);

        var catchBranch = (BlockExpr)Next(expr.CatchBody);
        tryBranch.IsRoot = expr.IsRoot;
        catchBranch.IsRoot = expr.IsRoot;

        return new TryExpr(
            tryBranch,
            catchBranch,
            expr.CatchIdentifier
        )
        {
            IsRoot = expr.IsRoot,
        };
    }

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

        var symbol = module.FindFunction(name, lookInImports: true);
        if (symbol == null)
            return null;

        if (module != _scope.ModuleScope && symbol.Expr.AccessLevel != AccessLevel.Public)
            throw new RuntimeAccessLevelException(symbol.Expr.AccessLevel, name);

        return symbol;
    }

    private void AddSemanticToken(SemanticTokenKind kind, Token token, SemanticFeature feature = SemanticFeature.None)
    {
        _semanticTokens?.Add(new SemanticToken(kind, token.Value, token.Position, feature));
    }

    private void AddSemanticTokens(SemanticTokenKind kind, IEnumerable<Token> tokens)
    {
        if (_semanticTokens == null)
            return;

        foreach (var token in tokens)
            _semanticTokens.Add(new SemanticToken(kind, token.Value, token.Position));
    }
}