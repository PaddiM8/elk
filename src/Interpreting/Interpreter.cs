#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Elk.Analysis;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.Bindings;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Interpreting;

partial class Interpreter
{
    public ShellEnvironment ShellEnvironment { get; }

    public ModuleScope CurrentModule
        => _scope.ModuleScope;

    public bool PrintErrors { get; init; } = true;

    public TextPos Position
        => _lastExpr?.Position ?? TextPos.Default;

    private Scope.Scope _scope;
    private readonly RootModuleScope _rootModule;
    private readonly ReturnHandler _returnHandler = new();
    private Expr? _lastExpr;

    public Interpreter(string? filePath)
    {
        ShellEnvironment = new ShellEnvironment();
        _rootModule = new(filePath);
        _scope = _rootModule;
    }

    public RuntimeObject Interpret(IList<Expr> ast, ModuleScope? scope = null, bool isEntireModule = false)
    {
        if (scope != null)
            _scope = scope;

        var previousScope = _scope;
        foreach (var module in _scope.ModuleScope.Modules
            .Concat(_scope.ModuleScope.ImportedModules)
            .Where(x => x != _scope)
            .Where(x => x.AnalysisStatus != AnalysisStatus.Failed && x.AnalysisStatus != AnalysisStatus.Evaluated))
        {
            _scope = module;
            if (module.AnalysisStatus == AnalysisStatus.None)
                Analyser.Analyse(module.Ast, module, isEntireModule: true);

            module.AnalysisStatus = AnalysisStatus.Evaluated;
            Interpret(module.Ast, module, isEntireModule: true);
        }

        _scope = previousScope;

        var analysedAst = Analyser.Analyse(ast, _scope.ModuleScope, isEntireModule);
        RuntimeObject lastResult = RuntimeNil.Value;
        try
        {
            foreach (var expr in analysedAst)
                lastResult = Next(expr);
        }
        catch (RuntimeException e)
        {
            e.Position = Position;
            _scope.ModuleScope.AnalysisStatus = AnalysisStatus.Failed;
            throw;
        }
        catch (InvalidOperationException e)
        {
            _scope.ModuleScope.AnalysisStatus = AnalysisStatus.Failed;

            // Sort/Order methods (eg. in the standard library) throw an exception when
            // they fail to compare two items. This should simply be a runtime error,
            // since that means the user is trying to compare values that can not be
            // compared with each other.
            // This has to be caught here due to generators being used.
            throw new RuntimeException(e.Message, Position);
        }

        return lastResult;
    }

    public RuntimeObject Interpret(string input, bool ownScope = false)
    {
        try
        {
            var ast = Parser.Parse(
                Lexer.Lex(input, _rootModule.FilePath, out var lexError),
                _scope
            );
            if (lexError != null)
                throw new RuntimeException(lexError.Message, lexError.Position);

            if (ownScope)
            {
                var block = new BlockExpr(
                    ast,
                    StructureKind.Other,
                    ast.FirstOrDefault()?.Position ?? TextPos.Default,
                    new LocalScope(_scope)
                );
                ast = new List<Expr> { block };
            }

            return Interpret(ast);
        }
        catch (RuntimeException e)
        {
            e.Position = Position;
            _lastExpr = null;
            _scope = _rootModule;
            throw;
        }
        catch (ParseException e)
        {
            _lastExpr = null;
            _scope = _rootModule;

            throw new RuntimeException(e.Message, e.Position);
        }
    }

    public bool ModuleExists(IEnumerable<string> modulePath)
        => _scope.ModuleScope.FindModule(modulePath, true) != null;

    public bool StructExists(string name)
        => _scope.ModuleScope.FindStruct(name, true) != null;

    public bool FunctionExists(string name, IEnumerable<string>? modulePath = null)
    {
        var module = modulePath == null
            ? _scope.ModuleScope
            : _scope.ModuleScope.FindModule(modulePath, true);

        return module?.FindFunction(name, true)?.Expr.AnalysisStatus
            is not (null or AnalysisStatus.Failed);
    }

    public bool VariableExists(string name)
        => _scope.ModuleScope.FindVariable(name) != null;

    public void AddGlobalVariable(string name, RuntimeObject value)
        => _scope.ModuleScope.AddVariable(name, value);

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
            ThrowExpr e => Visit(e),
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
        foreach (var value in expr.Entries)
        {
            var evaluatedValue = Next(value);
            dict.TryAdd(evaluatedValue.GetHashCode(), evaluatedValue);
        }

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
        var returnKind = expr.Kind switch
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
            return left.As<RuntimeBoolean>().IsTrue
                ? left
                : Next(expr.Right);
        }

        if (expr.Operator == OperationKind.And)
        {
            bool result = left.As<RuntimeBoolean>().IsTrue &&
                Next(expr.Right).As<RuntimeBoolean>().IsTrue;

            return RuntimeBoolean.From(result);
        }

        if (expr.Operator == OperationKind.Or)
        {
            bool result = left.As<RuntimeBoolean>().IsTrue ||
                Next(expr.Right).As<RuntimeBoolean>().IsTrue;

            return RuntimeBoolean.From(result);
        }

        var right = Next(expr.Right);
        if (expr.Operator == OperationKind.In)
        {
            bool result = right switch
            {
                RuntimeList list => list.Values
                    .Find(x => x.Operation(OperationKind.EqualsEquals, left).As<RuntimeBoolean>().IsTrue) != null,
                RuntimeRange range => range.Contains((int)left.As<RuntimeInteger>().Value),
                RuntimeSet set => set.Entries.ContainsKey(left.GetHashCode()),
                RuntimeDictionary dict => dict.Entries.ContainsKey(left.GetHashCode()),
                RuntimeString str => str.Value.Contains(left.As<RuntimeString>().Value),
                _ => throw new RuntimeInvalidOperationException("in", right.GetType()),
            };

            return RuntimeBoolean.From(result);
        }

        if (expr.Operator is OperationKind.EqualsEquals or OperationKind.NotEquals)
        {
            bool? areEqual = null;
            if (left is RuntimeNil && right is RuntimeNil)
                areEqual = true;
            else if (left is RuntimeNil || right is RuntimeNil)
                areEqual = false;
            else if (left is not (RuntimeBoolean or RuntimeInteger or RuntimeFloat or RuntimeString or RuntimePipe))
                areEqual = left == right;

            if (areEqual != null)
            {
                return expr.Operator == OperationKind.EqualsEquals
                    ? RuntimeBoolean.From(areEqual.Value)
                    : RuntimeBoolean.From(!areEqual.Value);
            }
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
        int? from = expr.From == null
            ? null
            : (int)Next(expr.From).As<RuntimeInteger>().Value;
        int? to = expr.To == null
            ? null
            : (int)Next(expr.To).As<RuntimeInteger>().Value;

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
        string name = expr.Identifier.Value;
        if (name.StartsWith('$'))
        {
            string? value = Environment.GetEnvironmentVariable(name[1..]);

            return value == null
                ? RuntimeNil.Value
                : new RuntimeString(value);
        }

        return expr.EnclosingClosureValue?.Environment.FindVariable(expr.Identifier.Value)?.Value
               ?? _scope.FindVariable(expr.Identifier.Value)?.Value
               ?? RuntimeNil.Value;
    }

    private RuntimeObject Visit(CallExpr expr, RuntimeClosureFunction? runtimeClosure = null)
    {
        if (expr is { IsReference: false, CallType: CallType.BuiltInTime })
            return EvaluateBuiltInTime(expr.Arguments);

        var evaluatedArguments = expr.Arguments.Select(Next).ToList();
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
                    globbingEnabled: expr.CallStyle == CallStyle.TextArguments
                ),
                CallType.StdFunction => EvaluateStdCall(arguments, expr.StdFunction!, runtimeClosure),
                CallType.Function => EvaluateFunctionCall(arguments, expr.FunctionSymbol!.Expr, expr.IsRoot, runtimeClosure),
                // Interpreter_BuiltIns.cs
                CallType.BuiltInCd => EvaluateBuiltInCd(arguments),
                CallType.BuiltInExec => EvaluateBuiltInExec(
                    arguments,
                    expr.RedirectionKind,
                    expr.DisableRedirectionBuffering,
                    globbingEnabled: expr.CallStyle == CallStyle.TextArguments
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
            string message = expr.CallType == CallType.Program
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
            return Evaluate(evaluatedArguments);
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

        int additionalsIndex = allArguments.Count;
        foreach (var parameter in stdFunction.Parameters.Reverse())
        {
            if (parameter.IsNullable)
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
            return new Func<RuntimeObject>(() => NextBlock(runtimeClosure.Expr.Body));

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject>(
                a =>
                {
                    if (parameters.Count > 0)
                        runtimeClosure.Environment.AddVariable(parameters[0], a);

                    return NextBlock(runtimeClosure.Expr.Body);
                });
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject, RuntimeObject>(
                (a, b) =>
                {
                    if (parameters.Count > 0)
                        runtimeClosure.Environment.AddVariable(parameters[0], a);

                    if (parameters.Count > 1)
                        runtimeClosure.Environment.AddVariable(parameters[1], b);

                    return NextBlock(runtimeClosure.Expr.Body);
                });
        }

        // Action
        if (closureFuncType == typeof(Action<RuntimeObject>))
        {
            return new Action<RuntimeObject>(
                a =>
                {
                    runtimeClosure.Expr.Body.IsRoot = true;
                    if (parameters.Count > 0)
                        runtimeClosure.Environment.AddVariable(parameters[0], a);

                    NextBlock(runtimeClosure.Expr.Body);
                });
        }

        if (closureFuncType == typeof(Action<RuntimeObject, RuntimeObject>))
        {
            return new Action<RuntimeObject, RuntimeObject>(
                (a, b) =>
                {
                    runtimeClosure.Expr.Body.IsRoot = true;
                    if (parameters.Count > 0)
                        runtimeClosure.Environment.AddVariable(parameters[0], a);

                    if (parameters.Count > 1)
                        runtimeClosure.Environment.AddVariable(parameters[1], b);

                    NextBlock(runtimeClosure.Expr.Body);
                });
        }

        // Fallback, variadic
        return new Func<IEnumerable<RuntimeObject>, RuntimeObject>(args =>
        {
            foreach (var (parameter, argument) in parameters.Zip(args))
                runtimeClosure.Environment.AddVariable(parameter, argument);

            return NextBlock(runtimeClosure.Expr.Body);
        });
    }

    private RuntimeObject EvaluateProgramCall(
        string fileName,
        List<RuntimeObject> arguments,
        RuntimeObject? pipedValue,
        RedirectionKind redirectionKind,
        bool disableRedirectionBuffering,
        bool globbingEnabled)
    {
        var newArguments = new List<string>();
        foreach (var argument in arguments)
        {
            string value = argument is RuntimeNil
                ? string.Empty
                : argument.As<RuntimeString>().Value;
            if (!globbingEnabled)
            {
                newArguments.Add(value);
                continue;
            }

            var matches = Globbing.Glob(ShellEnvironment.WorkingDirectory, value);
            if (matches.Any())
            {
                newArguments.AddRange(matches);
                continue;
            }

            newArguments.Add(value);
        }

        // Read potential shebang
        bool hasShebang = false;
        if (File.Exists(ShellEnvironment.GetAbsolutePath(fileName)))
        {
            using var streamReader = new StreamReader(ShellEnvironment.GetAbsolutePath(fileName));
            var firstChars = new char[2];
            streamReader.ReadBlock(firstChars, 0, 2);

            if (firstChars[0] == '#' && firstChars[1] == '!')
            {
                newArguments.Insert(0, fileName);
                fileName = streamReader.ReadLine() ?? "";
                hasShebang = true;
            }
        }

        if (!hasShebang && fileName.StartsWith("./"))
        {
            fileName = Path.Combine(ShellEnvironment.WorkingDirectory, fileName[2..]);
        }

        var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = fileName,
            RedirectStandardOutput = redirectionKind is RedirectionKind.Output or RedirectionKind.All,
            RedirectStandardError = redirectionKind is RedirectionKind.Error or RedirectionKind.All,
            RedirectStandardInput = pipedValue != null,
            WorkingDirectory = ShellEnvironment.WorkingDirectory,
        };

        foreach (var arg in newArguments)
            process.StartInfo.ArgumentList.Add(arg);

        var processContext = new ProcessContext(process, pipedValue);
        if (redirectionKind == RedirectionKind.None)
        {
            int exitCode = processContext.Start();

            return exitCode == 0
                ? RuntimeNil.Value
                : throw new RuntimeException("");
        }

        return new RuntimePipe(processContext, disableRedirectionBuffering);
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
                globbingEnabled: false
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

        return new RuntimeString(result.ToString());
    }

    private RuntimeObject Visit(ClosureExpr expr)
    {
        var scope = new LocalScope(
            expr.Function.EnclosingClosureValue?.Environment as Scope.Scope
                ?? (Scope.Scope)_scope.ModuleScope
        );
        foreach (var capture in expr.CapturedVariables)
        {
            var value = expr.Function.EnclosingClosureValue?.Environment.FindVariable(capture)?.Value
                ?? _scope.FindVariable(capture)?.Value
                ?? RuntimeNil.Value;
            scope.AddVariable(capture, value);
        }

        var runtimeClosure = new RuntimeClosureFunction(
            expr,
            scope,
            BuildRuntimeFunctionInvoker
        );
        expr.RuntimeValue = runtimeClosure;

        return expr.Function.IsReference
            ? runtimeClosure
            : NextCallWithClosure(expr.Function, runtimeClosure);
    }

    private RuntimeObject Visit(TryExpr expr)
    {
        RuntimeObject result;
        try
        {
            result = Next(expr.Body);
        }
        catch (RuntimeException e)
        {
            var value = e is RuntimeUserException userException
                ? userException.Value
                : new RuntimeString(e.Message);

            var scope = new LocalScope(_scope);
            if (expr.CatchIdentifier != null)
                scope.AddVariable(expr.CatchIdentifier.Value, value);

            result = NextBlock(expr.CatchBody, scope);
        }

        return result;
    }

    private RuntimeObject Visit(ThrowExpr expr)
        => throw new RuntimeUserException(Next(expr));
}