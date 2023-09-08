#region

using System;
using System.Collections.Generic;
using System.ComponentModel;
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

    public bool PrintErrors { get; init; } = true;

    private TextPos Position
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

    public RuntimeObject Interpret(List<Expr> ast, ModuleScope? scope = null)
    {
        if (scope != null)
            _scope = scope;

        List<Expr> analysedAst;
        try
        {
            analysedAst = Analyser.Analyse(ast, _scope.ModuleScope);
        }
        catch (AggregateException e)
        {
            if (!PrintErrors)
                throw;

            return (RuntimeError)e.Data["error"]!;
        }

        RuntimeObject lastResult = RuntimeNil.Value;
        try
        {
            foreach (var expr in analysedAst)
                lastResult = Next(expr);
        }
        catch (RuntimeException e)
        {
            var position = e.Position ?? Position;
            var error = new RuntimeError(e.Message, position);

            if (!PrintErrors)
                throw new AggregateException(error.ToString(), e);

            return error;
        }

        return lastResult;
    }

    public RuntimeObject Interpret(string input)
    {
        try
        {
            var ast = Parser.Parse(
                Lexer.Lex(input, _rootModule.FilePath, out var lexError),
                _scope
            );
            if (lexError != null)
                throw new ParseException(lexError.Position, lexError.Message);

            return Interpret(ast);
        }
        catch (ParseException e)
        {
            var error = new RuntimeError(e.Message, e.Position);

            if (!PrintErrors)
                throw new AggregateException(error.ToString(), e);

            return error;
        }
    }

    private RuntimeError Error(string message)
        => new(message, Position);

    public bool ModuleExists(IEnumerable<string> path)
        => _scope.ModuleScope.FindModule(path, true) != null;

    public bool StructExists(string name)
        => _scope.ModuleScope.FindStruct(name, true) != null;

    public bool FunctionExists(string name)
        => _scope.ModuleScope.FindFunction(name, true) != null;

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
            FunctionReferenceExpr e => Visit(e),
            StringInterpolationExpr e => Visit(e),
            ClosureExpr e => Visit(e),
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
            {
                return _returnHandler.Collect();
            }

            if (_returnHandler.ReturnKind == ReturnKind.ContinueLoop)
            {
                _returnHandler.Collect();
            }
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
        Debug.Assert(expr.Operator != OperationKind.Pipe);

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
            expr.Left.IsRoot = true;

            var leftAsRoot = Next(expr.Left);
            return leftAsRoot is RuntimeError
                ? leftAsRoot
                : Next(expr.Right);
        }

        if (expr.Operator == OperationKind.NonRedirectingOr)
        {
            expr.Left.IsRoot = true;

            var leftAsRoot = Next(expr.Left);
            return leftAsRoot is RuntimeError
                ? Next(expr.Right)
                : leftAsRoot;
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
                _ => throw new RuntimeInvalidOperationException("in", right.GetType().ToString()[7..]),
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
            else if (left is not (RuntimeBoolean or RuntimeInteger or RuntimeFloat or RuntimeString))
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
            if (Next(indexer.Value) is not IIndexable<RuntimeObject> indexable)
                throw new RuntimeUnableToIndexException(value.GetType());

            indexable[Next(indexer.Index)] = value;

            return value;
        }

        if (assignee is FieldAccessExpr fieldAccess)
        {
            var objectValue = Next(fieldAccess.Object);
            objectValue.As<RuntimeStruct>().Values[fieldAccess.Identifier.Value] = value;

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
        if (objectValue is not RuntimeStruct structValue)
            throw new RuntimeCastException(objectValue.GetType(), "Struct");

        if (structValue.Values.TryGetValue(expr.Identifier.Value, out var result))
            return result;

        throw new RuntimeNotFoundException(expr.Identifier.Value);
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
        var evaluatedArguments = expr.Arguments.Select(Next).ToList();
        RuntimeObject Evaluate(List<RuntimeObject> arguments)
        {
            return expr.CallType switch
            {
                CallType.Program => EvaluateProgramCall(
                    expr.Identifier.Value,
                    arguments,
                    expr.PipedToProgram != null
                        ? Next(expr.PipedToProgram)
                        : null,
                    globbingEnabled: expr.CallStyle == CallStyle.TextArguments,
                    isRoot: expr.IsRoot
                ),
                CallType.StdFunction => EvaluateStdCall(arguments, expr.StdFunction!, runtimeClosure),
                CallType.Function => EvaluateFunctionCall(arguments, expr.FunctionSymbol!.Expr, expr.IsRoot, runtimeClosure),
                // Interpreter_BuiltIns.cs
                CallType.BuiltInCd => EvaluateBuiltInCd(arguments),
                CallType.BuiltInExec => EvaluateBuiltInExec(
                    arguments,
                    globbingEnabled: expr.CallStyle == CallStyle.TextArguments,
                    isRoot: expr.IsRoot
                ),
                CallType.BuiltInScriptPath => EvaluateBuiltInScriptPath(arguments),
                CallType.BuiltInClosure => EvaluateBuiltInClosure((FunctionExpr)expr.EnclosingFunction!, arguments),
                CallType.BuiltInCall => EvaluateBuiltInCall(arguments, expr.IsRoot),
                CallType.BuiltInError => EvaluateBuiltInError(arguments),
                _ => throw new NotSupportedException(expr.CallType.ToString()),
            };
        }

        if (expr.Plurality == Plurality.Singular || evaluatedArguments.Count == 0)
            return Evaluate(evaluatedArguments);

        if (evaluatedArguments[0] is not IEnumerable<RuntimeObject> firstArguments)
            throw new RuntimeCastException(evaluatedArguments.GetType(), "Iterable");

        var results = new List<RuntimeObject>(evaluatedArguments.Count);
        foreach (var firstArgument in firstArguments.ToArray())
        {
            evaluatedArguments[0] = firstArgument;
            results.Add(Evaluate(evaluatedArguments));
        }

        return new RuntimeList(results);
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
                arguments.Count
            );
            allArguments.Add(variadicArguments);
            allArguments.AddRange(arguments.GetRange(0, stdFunction.VariadicStart.Value));
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
        catch (RuntimeStdException e)
        {
            return new RuntimeError(e.Message, Position);
        }
        catch (RuntimeException e)
        {
            throw new RuntimeException(e.Message, Position);
        }
        catch (Exception e)
        {
            return new RuntimeError($"Std: {e.Message}", Position);
        }
    }

    private object ConstructClosureFunc(Type closureFuncType, RuntimeClosureFunction runtimeClosure)
    {
        var parameters = runtimeClosure.Expr.Parameters;

        // TODO: Do something about this mess...
        if (closureFuncType == typeof(Func<RuntimeObject>))
        {
            return new Func<RuntimeObject>(() => NextBlock(runtimeClosure.Expr.Body));
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject>(
                a =>
                {
                    runtimeClosure.Environment.AddVariable(parameters[0].Value, a);

                    return NextBlock(runtimeClosure.Expr.Body);
                });
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject, RuntimeObject>(
                (a, b) =>
                {
                    runtimeClosure.Environment.AddVariable(parameters[0].Value, a);
                    runtimeClosure.Environment.AddVariable(parameters[1].Value, b);

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
                    runtimeClosure.Environment.AddVariable(parameters[0].Value, a);

                    NextBlock(runtimeClosure.Expr.Body);
                });
        }

        if (closureFuncType == typeof(Action<RuntimeObject, RuntimeObject>))
        {
            return new Action<RuntimeObject, RuntimeObject>(
                (a, b) =>
                {
                    runtimeClosure.Expr.Body.IsRoot = true;
                    runtimeClosure.Environment.AddVariable(parameters[0].Value, a);
                    runtimeClosure.Environment.AddVariable(parameters[1].Value, b);

                    NextBlock(runtimeClosure.Expr.Body);
                });
        }

        // Fallback, variadic
        return new Func<IEnumerable<RuntimeObject>, RuntimeObject>(
            args =>
            {
                foreach (var (parameter, argument) in runtimeClosure.Expr.Parameters.Zip(args))
                    runtimeClosure.Environment.AddVariable(parameter.Value, argument);

                return NextBlock(runtimeClosure.Expr.Body);
            });
    }

    private RuntimeObject EvaluateProgramCall(
        string fileName,
        List<RuntimeObject> arguments,
        RuntimeObject? pipedValue,
        bool globbingEnabled,
        bool isRoot)
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

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                RedirectStandardOutput = !isRoot,
                RedirectStandardError = !isRoot,
                RedirectStandardInput = pipedValue != null,
                WorkingDirectory = ShellEnvironment.WorkingDirectory,
            },
        };

        foreach (var arg in newArguments)
            process.StartInfo.ArgumentList.Add(arg);

        try
        {
            process.Start();
        }
        catch (Win32Exception)
        {
            throw new RuntimeNotFoundException(fileName);
        }

        if (pipedValue != null)
        {
            try
            {
                using var streamWriter = process.StandardInput;
                streamWriter.Write(pipedValue);
            }
            catch (IOException e)
            {
                throw new RuntimeException(e.Message);
            }
        }

        process.WaitForExit();

        if (!isRoot)
        {
            return process.ExitCode == 0
                ? new RuntimeString(process.StandardOutput.ReadToEnd())
                : Error(process.StandardError.ReadToEnd());
        }

        return RuntimeNil.Value;
    }

    private RuntimeObject Visit(LiteralExpr expr)
        => expr.RuntimeValue!;

    private RuntimeObject Visit(FunctionReferenceExpr functionReferenceExpr)
        => functionReferenceExpr.RuntimeFunction!;

    private RuntimeObject Visit(StringInterpolationExpr expr)
    {
        var result = new StringBuilder();
        foreach (var part in expr.Parts)
            result.Append(Next(part).As<RuntimeString>().Value);

        return new RuntimeString(result.ToString());
    }

    private RuntimeObject Visit(ClosureExpr closureExpr)
    {
        var scope = new LocalScope(
            closureExpr.Function.EnclosingClosureValue?.Environment as Scope.Scope
                ?? (Scope.Scope)_scope.ModuleScope
        );
        foreach (var capture in closureExpr.CapturedVariables)
        {
            var value = closureExpr.Function.EnclosingClosureValue?.Environment.FindVariable(capture)?.Value
                ?? _scope.FindVariable(capture)?.Value
                ?? RuntimeNil.Value;
            scope.AddVariable(capture, value);
        }

        var runtimeClosure = new RuntimeClosureFunction(closureExpr, scope);
        closureExpr.RuntimeValue = runtimeClosure;

        return closureExpr.Function is CallExpr callExpr
            ? NextCallWithClosure(callExpr, runtimeClosure)
            : runtimeClosure;
    }
}