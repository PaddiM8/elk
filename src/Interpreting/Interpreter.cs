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
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

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
    private ClosureExpr? _currentClosureExpr;

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
                Lexer.Lex(input, _rootModule.FilePath),
                _scope
            );

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

    public bool FunctionExists(string name)
        => _scope.ModuleScope.HasFunction(name);

    public bool VariableExists(string name)
        => _scope.ModuleScope.ContainsVariable(name);

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
            FunctionExpr => RuntimeNil.Value,
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
            _ => throw new ArgumentOutOfRangeException(nameof(expr), expr, null),
        };
    }

    private RuntimeObject NextBlock(BlockExpr blockExpr, bool clearScope = true)
    {
        if (_returnHandler.Active)
            return RuntimeNil.Value;

        _lastExpr = blockExpr;

        return Visit(blockExpr, clearScope);
    }

    private RuntimeObject NextCallWithClosure(CallExpr callExpr, ClosureExpr closureExpr)
    {
        if (_returnHandler.Active)
            return RuntimeNil.Value;

        _lastExpr = callExpr;

        return Visit(callExpr, closureExpr);
    }

    private RuntimeObject Visit(LetExpr expr)
    {
        SetVariables(expr.IdentifierList, Next(expr.Value));
        
        return RuntimeNil.Value;
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

        foreach (var current in enumerableValue)
        {
            expr.Branch.Scope.Clear();
            SetVariables(expr.IdentifierList, current, expr.Branch.Scope);
            NextBlock(expr.Branch, clearScope: false);

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

    private RuntimeObject Visit(DictionaryExpr expr)
    {
        var dict = new Dictionary<int, (RuntimeObject, RuntimeObject)>();
        foreach (var entry in expr.Entries)
        {
            var key = new RuntimeString(entry.Item1);
            dict.Add(key.GetHashCode(), (key, Next(entry.Item2)));
        }

        return new RuntimeDictionary(dict);
    }

    private RuntimeObject Visit(BlockExpr expr, bool clearScope = true)
    {
        var prevScope = _scope;
        _scope = expr.Scope;
        if (clearScope)
            expr.Scope.Clear();

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
        if (assignee is VariableExpr variable)
        {
            if (!_scope.UpdateVariable(variable.Identifier.Value, value))
            {
                throw new RuntimeNotFoundException(variable.Identifier.Value);
            }
        }
        else if (assignee is IndexerExpr indexer)
        {
            if (Next(indexer.Value) is not IIndexable<RuntimeObject> indexable)
                throw new RuntimeUnableToIndexException(value.GetType());

            indexable[Next(indexer.Index)] = value;
        }
        else
        {
            throw new RuntimeException("Invalid assignment");
        }

        return value;
    }

    private RuntimeObject Visit(UnaryExpr expr)
    {
        var value = Next(expr.Value);

        if (expr.Operator == OperationKind.Not)
            value = value.As<RuntimeBoolean>();

        return value.Operation(expr.Operator);
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
            if (to > from)
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
        if (expr.VariableSymbol == null)
        {
            string name = expr.Identifier.Value;
            string? value = Environment.GetEnvironmentVariable(name[1..]);

            return value == null
                ? RuntimeNil.Value
                : new RuntimeString(value);
        }

        return expr.VariableSymbol.Value;
    }

    private RuntimeObject Visit(CallExpr expr, ClosureExpr? closureExpr = null)
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
                CallType.StdFunction => EvaluateStdCall(arguments, expr.StdFunction!, closureExpr),
                CallType.Function => EvaluateFunctionCall(arguments, expr.FunctionSymbol!.Expr, expr.IsRoot, closureExpr),
                // Interpreter_BuiltIns.cs
                CallType.BuiltInCd => EvaluateBuiltInCd(arguments),
                CallType.BuiltInExec => EvaluateBuiltInExec(
                    arguments,
                    globbingEnabled: expr.CallStyle == CallStyle.TextArguments,
                    isRoot: expr.IsRoot
                ),
                CallType.BuiltInScriptPath => EvaluateBuiltInScriptPath(arguments),
                CallType.BuiltInClosure => EvaluateBuiltInClosure(arguments),
                CallType.BuiltInCall => EvaluateBuiltInCall(arguments, expr.IsRoot),
                CallType.BuiltInError => EvaluateBuiltInError(arguments),
                _ => throw new NotSupportedException(expr.CallType.ToString()),
            };
        }

        if (expr.Plurality == Plurality.Singular || evaluatedArguments.Count == 0)
            return Evaluate(evaluatedArguments);

        if (evaluatedArguments[0] is not IEnumerable<RuntimeObject> firstArguments)
            throw new RuntimeCastException(evaluatedArguments.GetType(), "iterable");

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
        ClosureExpr? closureExpr = null)
    {
        var allArguments = new List<RuntimeObject>(
            Math.Max(arguments.Count, function.Parameters.Count)
        );
        var functionScope = (LocalScope)function.Block.Scope;
        functionScope.Clear();
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

            functionScope.UpdateVariable(
                parameter!.Identifier.Value,
                allArguments.Last()
            );
        }

        function.Block.IsRoot = isRoot;

        var previousClosureExpr = _currentClosureExpr;
        _currentClosureExpr = closureExpr;
        var result = NextBlock(function.Block, clearScope: false);
        _currentClosureExpr = previousClosureExpr;

        return result;
    }

    private RuntimeObject EvaluateStdCall(
        List<RuntimeObject> arguments,
        StdFunction stdFunction,
        ClosureExpr? closureExpr = null)
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
                allArguments.Insert(additionalsIndex, ConstructClosureFunc(parameter.Type, closureExpr!));
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

    private object ConstructClosureFunc(Type closureFuncType, ClosureExpr closureExpr)
    {
        var parameters = closureExpr.Parameters;

        // TODO: Do something about this mess...
        if (closureFuncType == typeof(Func<RuntimeObject>))
        {
            return new Func<RuntimeObject>(() => NextBlock(closureExpr.Body));
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject>(
            a =>
            {
                var scope = closureExpr.Body.Scope;
                scope.Clear();
                scope.AddVariable(parameters[0].Value, a);

                return NextBlock(closureExpr.Body, clearScope: false);
            });
        }

        if (closureFuncType == typeof(Func<RuntimeObject, RuntimeObject, RuntimeObject>))
        {
            return new Func<RuntimeObject, RuntimeObject, RuntimeObject>(
            (a, b) =>
            {
                var scope = closureExpr.Body.Scope;
                scope.Clear();
                scope.AddVariable(parameters[0].Value, a);
                scope.AddVariable(parameters[1].Value, b);

                return NextBlock(closureExpr.Body, clearScope: false);
            });
        }

        return new Func<IEnumerable<RuntimeObject>, RuntimeObject>(
        args =>
        {
            var scope = closureExpr.Body.Scope;
            scope.Clear();
            foreach (var (parameter, argument) in closureExpr.Parameters.Zip(args))
                scope.AddVariable(parameter.Value, argument);

            return NextBlock(closureExpr.Body, clearScope: false);
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
            if (globbingEnabled)
            {
                var matcher = new Matcher();
                matcher.AddInclude(value);
                var result = matcher.Execute(
                    new DirectoryInfoWrapper(new DirectoryInfo(ShellEnvironment.WorkingDirectory))
                );

                if (result.HasMatches)
                {
                    newArguments.AddRange(result.Files.Select(x => x.Path));
                    continue;
                }
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
        => closureExpr.Function is CallExpr callExpr
            ? NextCallWithClosure(callExpr, closureExpr)
            : new RuntimeClosureFunction(closureExpr);
}