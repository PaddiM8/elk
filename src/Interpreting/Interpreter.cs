#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.Bindings;
using Elk.Std.DataTypes;
using Trace = Elk.Interpreting.Exceptions.Trace;

#endregion

namespace Elk.Interpreting;

partial class Interpreter
{
    public ShellEnvironment ShellEnvironment { get; }

    public ModuleScope CurrentModule
        => _scope.ModuleScope;

    public bool PrintErrors { get; init; } = true;

    public TextPos Position
        => _lastExpr?.StartPosition ?? TextPos.Default;

    private Scope.Scope _scope;
    private readonly RootModuleScope _rootModule;
    private readonly ReturnHandler _returnHandler = new();
    private Expr? _lastExpr;

    public Interpreter(string? filePath)
    {
        ShellEnvironment = new ShellEnvironment();
        _rootModule = new RootModuleScope(filePath, new Ast(Array.Empty<Expr>()));
        _scope = _rootModule;
    }

    public RuntimeObject Interpret(IList<Expr> ast, Scope.Scope scope)
    {
        var previousScope = _scope;
        _scope = scope;

        RuntimeObject lastResult = RuntimeNil.Value;
        try
        {
            foreach (var expr in ast)
                lastResult = Next(expr);
        }
        catch (RuntimeException e)
        {
            e.StartPosition = Position;
            e.EndPosition = _lastExpr?.EndPosition ?? TextPos.Default;
            _scope.ModuleScope.AnalysisStatus = AnalysisStatus.Failed;
            if (_lastExpr != null)
                e.ElkStackTrace.Insert(0, new Trace(_lastExpr.StartPosition, _lastExpr.EnclosingFunction));

            _lastExpr = null;
            _scope = _rootModule;

            throw;
        }
        catch (InvalidOperationException e)
        {
            _scope.ModuleScope.AnalysisStatus = AnalysisStatus.Failed;
            _lastExpr = null;
            _scope = _rootModule;

            // Sort/Order methods (eg. in the standard library) throw an exception when
            // they fail to compare two items. This should simply be a runtime error,
            // since that means the user is trying to compare values that can not be
            // compared with each other.
            // This has to be caught here due to generators being used.
            throw new RuntimeException(
                e.Message,
                Position,
                _lastExpr?.EndPosition ?? TextPos.Default
            );
        }

        _scope = previousScope;

        return lastResult;
    }

    private RuntimeObject Next(Expr expr)
    {
        if (_returnHandler.Active)
            return RuntimeNil.Value;

        _lastExpr = expr;

        return expr switch
        {
            ModuleExpr => RuntimeNil.Value,
            StructExpr => RuntimeNil.Value,
            FunctionExpr => RuntimeNil.Value,
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
            _ => throw new ArgumentOutOfRangeException(nameof(expr), expr, null),
        };
    }

    private RuntimeObject NextBlock(BlockExpr blockExpr, LocalScope? scope = null)
    {
        if (_returnHandler.Active)
            return RuntimeNil.Value;

        _lastExpr = blockExpr;

        return Visit(blockExpr, scope);
    }

    private RuntimeObject NextCallWithClosure(CallExpr callExpr, RuntimeClosureFunction runtimeClosure)
    {
        if (_returnHandler.Active)
            return RuntimeNil.Value;

        _lastExpr = callExpr;

        return Visit(callExpr, runtimeClosure);
    }

    private RuntimeObject Visit(LetExpr expr)
    {
        SetVariables(expr.IdentifierList, Next(expr.Value));

        return RuntimeNil.Value;
    }

    private RuntimeObject Visit(NewExpr expr)
    {
        var dict = new Dictionary<string, RuntimeObject>();
        IEnumerable<string> parameters;
        if (expr.StructSymbol!.Expr != null)
        {
            parameters = expr
                .StructSymbol
                .Expr!
                .Parameters
                .Select(x => x.Identifier.Value);
        }
        else
        {
            parameters = expr
                .StructSymbol
                .StdStruct!
                .Parameters
                .Select(x => x.Name);
        }

        var arguments = expr.Arguments.Select(Next);
        foreach (var (key, value) in parameters.Zip(arguments))
            dict[key] = value;

        return new RuntimeStruct(expr.StructSymbol, dict);
    }

    private void SetVariables(IReadOnlyCollection<Token> identifiers, RuntimeObject value, Scope.Scope? scope = null)
    {
        if (identifiers.Count > 1)
        {
            if (value is not IEnumerable<RuntimeObject> values)
            {
                throw new RuntimeException("Unable to deconstruct value");
            }

            foreach (var (identifier, deconstructedValue) in identifiers.Zip(values))
            {
                SetVariable(identifier.Value, deconstructedValue, scope ?? _scope);
            }
        }
        else
        {
            SetVariable(identifiers.First().Value, value, scope ?? _scope);
        }
    }

    private void SetVariable(string name, RuntimeObject value, Scope.Scope scope)
    {
        if (name.StartsWith('$'))
        {
            Environment.SetEnvironmentVariable(
                name[1..],
                value.As<RuntimeString>().Value
            );
        }
        else
        {
            scope.AddVariable(name, value);
        }
    }

    private RuntimeObject Visit(IfExpr expr)
    {
        var condition = Next(expr.Condition);
        var conditionValue = condition.As<RuntimeBoolean>();
        if (conditionValue.IsTrue)
            return Next(expr.ThenBranch);

        return expr.ElseBranch == null
            ? RuntimeNil.Value
            : Next(expr.ElseBranch);
    }

    private RuntimeObject Visit(ForExpr expr)
    {
        var value = Next(expr.Value);

        if (value is not IEnumerable<RuntimeObject> enumerableValue)
            throw new RuntimeIterationException(value.GetType());

        var scope = new LocalScope(_scope);
        foreach (var current in enumerableValue)
        {
            SetVariables(expr.IdentifierList, current, scope);
            NextBlock(expr.Branch, scope);

            if (_returnHandler.ReturnKind == ReturnKind.BreakLoop)
                return _returnHandler.Collect();

            if (_returnHandler.ReturnKind == ReturnKind.ContinueLoop)
                _returnHandler.Collect();
        }

        return RuntimeNil.Value;
    }

    private RuntimeObject Visit(WhileExpr expr)
    {
        while (Next(expr.Condition).As<RuntimeBoolean>().IsTrue)
        {
            Next(expr.Branch);

            if (_returnHandler.ReturnKind == ReturnKind.BreakLoop)
                return _returnHandler.Collect();

            if (_returnHandler.ReturnKind == ReturnKind.ContinueLoop)
                _returnHandler.Collect();
        }

        return RuntimeNil.Value;
    }

    private RuntimeObject Visit(TupleExpr expr)
    {
        return new RuntimeTuple(expr.Values.Select(Next).ToList());
    }

    private RuntimeObject Visit(ListExpr expr)
    {
        return new RuntimeList(expr.Values.Select(Next).ToList());
    }

    private RuntimeObject Visit(SetExpr expr)
    {
        var dict = new Dictionary<int, RuntimeObject>(expr.Entries.Count);
        foreach (var evaluatedValue in expr.Entries.Select(Next))
            dict.TryAdd(evaluatedValue.GetHashCode(), evaluatedValue);

        return new RuntimeSet(dict);
    }

    private RuntimeObject Visit(DictionaryExpr expr)
    {
        var dict = new Dictionary<int, (RuntimeObject, RuntimeObject)>(expr.Entries.Count);
        foreach (var (key, value) in expr.Entries)
        {
            var evaluatedKey = Next(key);
            if (!dict.TryAdd(evaluatedKey.GetHashCode(), (evaluatedKey, Next(value))))
                throw new RuntimeException("Duplicate value in dictionary");
        }

        return new RuntimeDictionary(dict);
    }

    private RuntimeObject Visit(BlockExpr expr, LocalScope? scope = null)
    {
        var prevScope = _scope;
        _scope = scope ?? new LocalScope(_scope);

        RuntimeObject lastValue = RuntimeNil.Value;
        foreach (var (child, i) in expr.Expressions.WithIndex())
        {
            // If there is a value to be returned, stop immediately
            // and make sure it's passed upwards.
            if (BlockShouldExit(expr, out var returnValue))
            {
                if (returnValue != null)
                    lastValue = returnValue;

                break;
            }

            // If last
            if (i == expr.Expressions.Count - 1)
            {
                child.IsRoot = expr.IsRoot;
                lastValue = Next(child);

                break;
            }

            Next(child);
        }

        _scope = prevScope;

        BlockShouldExit(expr, out var explicitReturnValue);

        return explicitReturnValue ?? lastValue;
    }

    private bool BlockShouldExit(BlockExpr expr, out RuntimeObject? returnValue)
    {
        returnValue = null;
        if (!_returnHandler.Active)
            return false;

        if (expr.ParentStructureKind == StructureKind.Function &&
            _returnHandler.ReturnKind == ReturnKind.ReturnFunction)
        {
            returnValue = _returnHandler.Collect();
        }

        return true;
    }

    private RuntimeObject Visit(KeywordExpr expr)
    {
        if (expr.Keyword.Kind == TokenKind.Throw)
        {
            RuntimeObject errorValue = expr.Value == null
                ? RuntimeNil.Value
                : Next(expr.Value);
            var runtimeError = errorValue as RuntimeError
                ?? new RuntimeError(errorValue);

            throw new RuntimeUserException(runtimeError);
        }

        var returnKind = expr.Keyword.Kind switch
        {
            TokenKind.Break => ReturnKind.BreakLoop,
            TokenKind.Continue => ReturnKind.ContinueLoop,
            TokenKind.Return => ReturnKind.ReturnFunction,
            _ => throw new ArgumentOutOfRangeException(),
        };
        var value = expr.Value == null
            ? RuntimeNil.Value
            : Next(expr.Value);

        _returnHandler.TriggerReturn(returnKind, value);

        return RuntimeNil.Value;
    }

    private RuntimeObject Visit(BinaryExpr expr)
    {
        Debug.Assert(expr.Operator is not (OperationKind.Pipe or OperationKind.PipeErr or OperationKind.PipeAll));

        if (expr.Operator == OperationKind.Equals)
        {
            return EvaluateAssignment(expr.Left, Next(expr.Right));
        }

        if (expr.Operator == OperationKind.If)
        {
            return Next(expr.Right).As<RuntimeBoolean>().IsTrue
                ? Next(expr.Left)
                : RuntimeNil.Value;
        }

        if (expr.Operator == OperationKind.NonRedirectingAnd)
        {
            Next(expr.Left);

            return Next(expr.Right);
        }

        if (expr.Operator == OperationKind.NonRedirectingOr)
        {
            try
            {
                return Next(expr.Left);
            }
            catch (RuntimeException)
            {
                return Next(expr.Right);
            }
        }

        var left = Next(expr.Left);
        if (expr.Operator == OperationKind.Coalescing)
        {
            return left is RuntimeNil
                ? Next(expr.Right)
                : left;
        }

        if (expr.Operator == OperationKind.And)
        {
            return !left.As<RuntimeBoolean>().IsTrue
                ? left
                : Next(expr.Right);
        }

        if (expr.Operator == OperationKind.Or)
        {
            return left.As<RuntimeBoolean>().IsTrue
                ? left
                : Next(expr.Right);
        }

        var right = Next(expr.Right);
        if (expr.Operator == OperationKind.In)
        {
            var result = right switch
            {
                RuntimeList list => list.Values
                    .Find(x => x.Operation(OperationKind.EqualsEquals, left).As<RuntimeBoolean>().IsTrue) != null,
                RuntimeRange range => range.Contains(left.As<RuntimeInteger>().Value),
                RuntimeSet set => set.Entries.ContainsKey(left.GetHashCode()),
                RuntimeDictionary dict => dict.Entries.ContainsKey(left.GetHashCode()),
                RuntimeString str => str.Value.Contains(left.As<RuntimeString>().Value),
                _ => throw new RuntimeInvalidOperationException("in", right.GetType()),
            };

            return RuntimeBoolean.From(result);
        }

        if (expr.Operator is OperationKind.EqualsEquals)
        {
            var isLeftNil = left is RuntimeNil;
            var isRightNil = right is RuntimeNil;
            if (isLeftNil != isRightNil && (isLeftNil || isRightNil))
                return RuntimeBoolean.False;

            return RuntimeBoolean.From(left.Equals(right));
        }

        if (expr.Operator is OperationKind.NotEquals)
        {
            var isLeftNil = left is RuntimeNil;
            var isRightNil = right is RuntimeNil;
            if (isLeftNil != isRightNil && (isLeftNil || isRightNil))
                return RuntimeBoolean.True;

            return RuntimeBoolean.From(!left.Equals(right));
        }

        return expr.Operator switch
        {
            OperationKind.Modulo => left.As<RuntimeInteger>().Operation(expr.Operator, right.As<RuntimeInteger>()),
            OperationKind.And or OperationKind.Or => left.As<RuntimeBoolean>().Operation(expr.Operator, right.As<RuntimeBoolean>()),
            _ => left.Operation(expr.Operator, right),
        };
    }

    private RuntimeObject EvaluateAssignment(Expr assignee, RuntimeObject value)
    {
        if (assignee is VariableExpr variableExpr)
        {
            var enclosingClosure = variableExpr.EnclosingClosureValue;
            if (enclosingClosure != null)
            {
                enclosingClosure.Environment.UpdateVariable(variableExpr.Identifier.Value, value);

                return value;
            }

            _scope.UpdateVariable(variableExpr.Identifier.Value, value);

            return value;
        }

        if (assignee is IndexerExpr indexer)
        {
            var evaluatedIndexer = Next(indexer.Value);
            if (evaluatedIndexer is not IIndexable<RuntimeObject> indexable)
                throw new RuntimeUnableToIndexException(evaluatedIndexer.GetType());

            indexable[Next(indexer.Index)] = value;

            return value;
        }

        if (assignee is FieldAccessExpr fieldAccess)
        {
            var objectValue = Next(fieldAccess.Object);
            if (objectValue is RuntimeDictionary dict)
            {
                dict.Entries[fieldAccess.RuntimeIdentifier!.GetHashCode()] = (fieldAccess.RuntimeIdentifier, value);

                return value;
            }

            if (objectValue is RuntimeStruct structValue)
            {
                if (!structValue.Values.ContainsKey(fieldAccess.Identifier.Value))
                    throw new RuntimeNotFoundException(fieldAccess.Identifier.Value);

                structValue.Values[fieldAccess.Identifier.Value] = value;

                return value;
            }

            if (objectValue is not IIndexable<RuntimeObject> indexable)
                throw new RuntimeCastException(objectValue.GetType(), "Indexable");

            indexable[fieldAccess.RuntimeIdentifier!] = value;

            return value;
        }

        throw new RuntimeException("Invalid assignment");
    }

    private RuntimeObject Visit(UnaryExpr expr)
    {
        var value = Next(expr.Value);

        if (expr.Operator == OperationKind.Not)
            value = value.As<RuntimeBoolean>();

        return value.Operation(expr.Operator);
    }

    private RuntimeObject Visit(FieldAccessExpr expr)
    {
        var objectValue = Next(expr.Object);
        if (objectValue is RuntimeDictionary dict)
        {
            return dict.Entries.TryGetValue(expr.RuntimeIdentifier!.GetHashCode(), out var result)
                ? result.Item2
                : throw new RuntimeNotFoundException(expr.Identifier.Value);
        }

        if (objectValue is RuntimeStruct structValue)

        {
            return structValue.Values.TryGetValue(expr.Identifier.Value, out var result)
                ? result
                : throw new RuntimeNotFoundException(expr.Identifier.Value);
        }

        if (objectValue is not IIndexable<RuntimeObject> indexable)
            throw new RuntimeCastException(objectValue.GetType(), "Indexable");

        return indexable[expr.RuntimeIdentifier!];
    }

    private RuntimeObject Visit(RangeExpr expr)
    {
        long? from = expr.From == null
            ? null
            : Next(expr.From).As<RuntimeInteger>().Value;
        long? to = expr.To == null
            ? null
            : Next(expr.To).As<RuntimeInteger>().Value;

        if (expr.Inclusive)
        {
            if (to >= from)
            {
                to++;
            }
            else
            {
                from++;
            }
        }

        return new RuntimeRange(from, to);
    }

    private RuntimeObject Visit(IndexerExpr expr)
    {
        var value = Next(expr.Value);
        if (value is IIndexable<RuntimeObject> indexableValue)
            return indexableValue[Next(expr.Index)];

        throw new RuntimeUnableToIndexException(value.GetType());
    }

    private RuntimeObject Visit(TypeExpr expr)
        => expr.RuntimeValue!;

    private RuntimeObject Visit(VariableExpr expr)
    {
        var name = expr.Identifier.Value;
        if (name.StartsWith('$'))
        {
            var value = Environment.GetEnvironmentVariable(name[1..]);

            return value == null
                ? RuntimeNil.Value
                : new RuntimeString(value);
        }

        var result = _scope.FindVariable(expr.Identifier.Value)?.Value
               ?? expr.EnclosingClosureValue?.Environment.FindVariable(expr.Identifier.Value)?.Value
               ?? RuntimeNil.Value;

        return result;
    }

    private RuntimeObject Visit(CallExpr expr, RuntimeClosureFunction? runtimeClosure = null)
    {
        try
        {
            return EvaluateCall(expr, runtimeClosure);
        }
        catch (RuntimeException ex)
        {
            if (_lastExpr != null)
                ex.ElkStackTrace.Add(new Trace(expr.StartPosition, expr.EnclosingFunction));

            throw;
        }
    }

    private RuntimeObject EvaluateCall(CallExpr expr, RuntimeClosureFunction? runtimeClosure = null)
    {
        if (expr is { IsReference: false, CallType: CallType.BuiltInTime })
            return EvaluateBuiltInTime(expr.Arguments);

        var partlyEvaluatedArguments = expr.Arguments.Select(Next);
        List<RuntimeObject> evaluatedArguments;
        if (expr.CallStyle == CallStyle.TextArguments)
        {
            evaluatedArguments = [];
            foreach (var argument in partlyEvaluatedArguments)
            {
                if (argument is not RuntimeString stringArgument)
                {
                    evaluatedArguments.Add(argument);
                    continue;
                }

                var matches = stringArgument.IsTextArgument
                    ? Globbing.Glob(ShellEnvironment.WorkingDirectory, stringArgument.Value).ToList()
                    : [];
                if (matches.Any())
                {
                    evaluatedArguments.AddRange(
                        matches.Select(x => new RuntimeString(x))
                    );

                    continue;
                }

                evaluatedArguments.Add(argument);
            }
        }
        else
        {
            evaluatedArguments = partlyEvaluatedArguments.ToList();
        }

        RuntimeObject Evaluate(List<RuntimeObject> arguments)
        {
            if (expr is { RedirectionKind: RedirectionKind.None, IsRoot: false })
                expr.RedirectionKind = RedirectionKind.Output;

            var piped = expr.PipedToProgram == null
                ? null
                : Next(expr.PipedToProgram);

            return expr.CallType switch
            {
                CallType.Program => EvaluateProgramCall(
                    expr.Identifier.Value,
                    arguments,
                    piped,
                    expr.RedirectionKind,
                    expr.DisableRedirectionBuffering,
                    expr.AutomaticStart,
                    expr.EnvironmentVariables.Select(x => (x.Key, Next(x.Value)))
                ),
                CallType.StdFunction => EvaluateStdCall(arguments, expr.StdFunction!, runtimeClosure),
                CallType.Function => EvaluateFunctionCall(arguments, expr.FunctionSymbol!.Expr, expr.IsRoot, runtimeClosure),
                // Interpreter_BuiltIns.cs
                CallType.BuiltInCd => EvaluateBuiltInCd(arguments),
                CallType.BuiltInExec => EvaluateBuiltInExec(
                    arguments,
                    expr.RedirectionKind,
                    expr.DisableRedirectionBuffering
                ),
                CallType.BuiltInScriptPath => EvaluateBuiltInScriptPath(arguments),
                CallType.BuiltInClosure => EvaluateBuiltInClosure((FunctionExpr)expr.EnclosingFunction!, arguments),
                CallType.BuiltInCall => EvaluateBuiltInCall(arguments, expr.IsRoot),
                _ => throw new NotSupportedException(expr.CallType.ToString()),
            };
        }

        if (expr.IsReference)
        {
            var arguments = expr.Arguments.Select(Next);

            return expr.CallType switch
            {
                CallType.Program => new RuntimeProgramFunction(
                    expr.Identifier.Value,
                    arguments,
                    expr.Plurality,
                    BuildRuntimeFunctionInvoker
                ),
                CallType.StdFunction => new RuntimeStdFunction(
                    expr.StdFunction!,
                    arguments,
                    expr.Plurality,
                    BuildRuntimeFunctionInvoker
                ),
                CallType.Function => new RuntimeSymbolFunction(
                    expr.FunctionSymbol!,
                    arguments,
                    expr.Plurality,
                    BuildRuntimeFunctionInvoker
                ),
                _ => throw new RuntimeException("Cannot turn built-in functions (such as cd, exec, call) into function references."),
            };
        }

        if (expr.Plurality == Plurality.Singular || evaluatedArguments.Count == 0)
            return Evaluate(evaluatedArguments);

        if (evaluatedArguments.First() is not IEnumerable<RuntimeObject> firstArguments)
        {
            var message = expr.CallType == CallType.Program
                ? "Note: The call with plurality was evaluated as a program call, since a function with that name could not be found."
                : "";
            throw new RuntimeCastException(
                evaluatedArguments.First().GetType(),
                "Iterable",
                message
            );
        }

        var evaluatedWithPlurality = firstArguments.Select(x =>
        {
            evaluatedArguments[0] = x;

            try
            {
                return Evaluate(evaluatedArguments);
            }
            catch (RuntimeException ex)
            {
                if (_lastExpr != null)
                    ex.ElkStackTrace.Add(new Trace(expr.StartPosition, expr.EnclosingFunction));

                throw;
            }
        });

        return new RuntimeList(evaluatedWithPlurality);
    }

    private Invoker BuildRuntimeFunctionInvoker(RuntimeFunction function)
    {
        return (invokerArguments, invokerIsRoot) => EvaluateBuiltInCall(function, invokerArguments, invokerIsRoot);
    }

    private RuntimeObject EvaluateFunctionCall(
        IReadOnlyCollection<RuntimeObject> arguments,
        FunctionExpr function,
        bool isRoot,
        RuntimeClosureFunction? givenClosure = null)
    {
        var allArguments = new List<RuntimeObject>(
            Math.Max(arguments.Count, function.Parameters.Count)
        );
        var functionScope = new LocalScope(function.Block.Scope.Parent);
        foreach (var (parameter, argument) in function.Parameters.ZipLongest(arguments))
        {
            if (argument == null && parameter?.DefaultValue != null)
            {
                allArguments.Add(Next(parameter.DefaultValue));
            }
            else if (argument != null)
            {
                allArguments.Add(argument);
            }
            else
            {
                continue;
            }

            functionScope.AddVariable(
                parameter!.Identifier.Value,
                allArguments.Last()
            );
        }

        function.GivenClosure = givenClosure;
        function.Block.IsRoot = isRoot;

        return NextBlock(function.Block, functionScope);
    }

    private RuntimeObject EvaluateStdCall(
        List<RuntimeObject> arguments,
        StdFunction stdFunction,
        RuntimeClosureFunction? runtimeClosure = null)
    {
        var allArguments = new List<object?>(arguments.Count + 2);
        if (stdFunction.VariadicStart.HasValue)
        {
            var variadicArguments = arguments.GetRange(
                stdFunction.VariadicStart.Value,
                arguments.Count - stdFunction.VariadicStart.Value
            );
            allArguments.AddRange(arguments.GetRange(0, stdFunction.VariadicStart.Value));
            allArguments.Add(variadicArguments);
        }
        else
        {
            allArguments.AddRange(arguments);
        }

        var additionalsIndex = allArguments.Count;
        foreach (var parameter in stdFunction.Parameters.Reverse())
        {
            if (parameter.IsNullable && allArguments.Count < stdFunction.Parameters.Length)
                allArguments.Insert(additionalsIndex, null);
            else if (parameter.Type == typeof(ShellEnvironment))
                allArguments.Insert(additionalsIndex, ShellEnvironment);
            else if (parameter.IsClosure)
                allArguments.Insert(additionalsIndex, ConstructClosureFunc(parameter.Type, runtimeClosure!));
        }

        try
        {
            return stdFunction.Invoke(allArguments);
        }
        catch (RuntimeException e)
        {
            throw new RuntimeStdException(e.Message)
            {
                ElkStackTrace = e.ElkStackTrace,
            };
        }
        catch (Exception e)
        {
            throw new RuntimeStdException(e.Message);
        }
    }

    private object ConstructClosureFunc(Type closureFuncType, RuntimeClosureFunction runtimeClosure)
    {
        var parameters = runtimeClosure.Expr.Parameters
            .Select(x => x.Value)
            .ToList();

        // TODO: Do something about this mess...
        if (closureFuncType == typeof(Func<RuntimeObject>))
        {
            var scope = new LocalScope(runtimeClosure.Expr.Body.Scope.ModuleScope);

            return new Func<RuntimeObject>(() =>
                {
                    var previousRuntimeValue = runtimeClosure.Expr.RuntimeValue;
                    runtimeClosure.Expr.RuntimeValue = runtimeClosure;
                    var result = NextBlock(runtimeClosure.Expr.Body, scope);
                    runtimeClosure.Expr.RuntimeValue = previousRuntimeValue;

                    return result;
                }
            );
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject>(
                a =>
                {
                    var scope = new LocalScope(runtimeClosure.Expr.Body.Scope.ModuleScope);
                    if (parameters.Count > 0)
                        scope.AddVariable(parameters[0], a);

                    var previousRuntimeValue = runtimeClosure.Expr.RuntimeValue;
                    runtimeClosure.Expr.RuntimeValue = runtimeClosure;
                    var result = NextBlock(runtimeClosure.Expr.Body, scope);
                    runtimeClosure.Expr.RuntimeValue = previousRuntimeValue;

                    return result;
                });
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject, RuntimeObject>(
                (a, b) =>
                {
                    var scope = new LocalScope(runtimeClosure.Expr.Body.Scope.ModuleScope);
                    if (parameters.Count > 0)
                        scope.AddVariable(parameters[0], a);

                    if (parameters.Count > 1)
                        scope.AddVariable(parameters[1], b);

                    var previousRuntimeValue = runtimeClosure.Expr.RuntimeValue;
                    runtimeClosure.Expr.RuntimeValue = runtimeClosure;
                    var result = NextBlock(runtimeClosure.Expr.Body, scope);
                    runtimeClosure.Expr.RuntimeValue = previousRuntimeValue;

                    return result;
                });
        }

        // Action
        if (closureFuncType == typeof(Action<RuntimeObject>))
        {
            return new Action<RuntimeObject>(
                a =>
                {
                    var scope = new LocalScope(runtimeClosure.Expr.Body.Scope.ModuleScope);
                    runtimeClosure.Expr.Body.IsRoot = true;
                    if (parameters.Count > 0)
                        scope.AddVariable(parameters[0], a);

                    var previousRuntimeValue = runtimeClosure.Expr.RuntimeValue;
                    runtimeClosure.Expr.RuntimeValue = runtimeClosure;
                    NextBlock(runtimeClosure.Expr.Body, scope);
                    runtimeClosure.Expr.RuntimeValue = previousRuntimeValue;
                });
        }

        if (closureFuncType == typeof(Action<RuntimeObject, RuntimeObject>))
        {
            return new Action<RuntimeObject, RuntimeObject>(
                (a, b) =>
                {
                    var scope = new LocalScope(runtimeClosure.Expr.Body.Scope.ModuleScope);
                    runtimeClosure.Expr.Body.IsRoot = true;
                    if (parameters.Count > 0)
                        scope.AddVariable(parameters[0], a);

                    if (parameters.Count > 1)
                        scope.AddVariable(parameters[1], b);

                    var previousRuntimeValue = runtimeClosure.Expr.RuntimeValue;
                    runtimeClosure.Expr.RuntimeValue = runtimeClosure;
                    NextBlock(runtimeClosure.Expr.Body, scope);
                    runtimeClosure.Expr.RuntimeValue = previousRuntimeValue;
                });
        }

        // Fallback, variadic
        return new Func<IEnumerable<RuntimeObject>, RuntimeObject>(args =>
        {
            var scope = new LocalScope(runtimeClosure.Expr.Body.Scope.ModuleScope);
            foreach (var (parameter, argument) in parameters.Zip(args))
                scope.AddVariable(parameter, argument);

            var previousRuntimeValue = runtimeClosure.Expr.RuntimeValue;
            runtimeClosure.Expr.RuntimeValue = runtimeClosure;
            var result = NextBlock(runtimeClosure.Expr.Body, scope);
            runtimeClosure.Expr.RuntimeValue = previousRuntimeValue;

            return result;
        });
    }

    private RuntimeObject EvaluateProgramCall(
        string fileName,
        List<RuntimeObject> arguments,
        RuntimeObject? pipedValue,
        RedirectionKind redirectionKind,
        bool disableRedirectionBuffering,
        bool automaticStart,
        IEnumerable<(string, RuntimeObject)>? environmentVariables)
    {
        var newArguments = arguments.Select(argument =>
                argument is RuntimeNil
                    ? string.Empty
                    : argument.As<RuntimeString>().Value
            );

        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName.StartsWith("./")
                ? Path.Combine(ShellEnvironment.WorkingDirectory, fileName)
                : fileName,
            RedirectStandardOutput = redirectionKind is RedirectionKind.Output or RedirectionKind.All,
            RedirectStandardError = redirectionKind is RedirectionKind.Error or RedirectionKind.All,
            RedirectStandardInput = pipedValue != null,
            WorkingDirectory = ShellEnvironment.WorkingDirectory,
        };

        if (environmentVariables != null)
        {
            foreach (var (key, value) in environmentVariables)
                process.StartInfo.EnvironmentVariables.Add(key, value.ToString());
        }

        foreach (var arg in newArguments)
            process.StartInfo.ArgumentList.Add(arg);

        var processContext = new ProcessContext(
            process,
            pipedValue,
            waitForExit: !disableRedirectionBuffering
        );
        if (redirectionKind == RedirectionKind.None)
        {
            processContext.Start();

            return RuntimeNil.Value;
        }

        return new RuntimePipe(processContext, disableRedirectionBuffering, automaticStart);
    }

    private RuntimeObject Visit(LiteralExpr expr)
    {
        if (expr.Value.Kind == TokenKind.BashLiteral)
        {
            var arguments = new List<RuntimeObject>
            {
                new RuntimeString("-c"),
                new RuntimeString(expr.Value.Value[2..]),
            };
            EvaluateProgramCall(
                "bash",
                arguments,
                null,
                RedirectionKind.None,
                disableRedirectionBuffering: false,
                automaticStart: true,
                environmentVariables: null
            );

            return RuntimeNil.Value;
        }

        return expr.RuntimeValue!;
    }

    private RuntimeObject Visit(StringInterpolationExpr expr)
    {
        var result = new StringBuilder();
        foreach (var part in expr.Parts)
            result.Append(Next(part).As<RuntimeString>().Value);

        return new RuntimeString(result.ToString())
        {
            IsTextArgument = expr.IsTextArgument,
        };
    }

    private RuntimeObject Visit(ClosureExpr expr)
    {
        var scope = new LocalScope(
            expr.Function.EnclosingClosureValue?.Environment as Scope.Scope
                ?? _scope.ModuleScope
        );
        foreach (var capture in expr.CapturedVariables)
        {
            var value = _scope.FindVariable(capture)?.Value
                ?? expr.Function.EnclosingClosureValue?.Environment.FindVariable(capture)?.Value
                ?? RuntimeNil.Value;
            scope.AddVariable(capture, value);
        }

        var runtimeClosure = new RuntimeClosureFunction(
            expr,
            scope,
            BuildRuntimeFunctionInvoker
        );

        var previousRuntimeValue = expr.RuntimeValue;
        expr.RuntimeValue = runtimeClosure;

        var result = expr.Function.IsReference
            ? runtimeClosure
            : NextCallWithClosure(expr.Function, runtimeClosure);
        expr.RuntimeValue = previousRuntimeValue;

        return result;
    }

    private RuntimeObject Visit(TryExpr expr)
    {
        try
        {
            return Next(expr.Body);
        }
        catch (RuntimeException ex)
        {
            var value = ex is RuntimeUserException userException
                ? userException.Value
                : new RuntimeError(new RuntimeString(ex.Message));
            value.StackTrace = ex.ElkStackTrace;

            foreach (var catchExpression in expr.CatchExpressions)
            {
                var type = catchExpression.Type == null
                    ? null
                    : (RuntimeType)Next(catchExpression.Type);
                if (type?.Type == value.Value.GetType())
                    continue;

                var scope = new LocalScope(_scope);
                if (catchExpression.Identifier != null)
                    scope.AddVariable(catchExpression.Identifier.Value, value);

                return NextBlock(catchExpression.Body, scope);
            }

            throw;
        }
    }
}