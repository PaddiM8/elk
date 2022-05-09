using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Elk.Lexing;
using Elk.Parsing;

namespace Elk.Interpreting;

class Interpreter
{
    public ShellEnvironment ShellEnvironment { get; }

    public bool PrintErrors { get; init; } = true;

    private Scope.Scope _scope;
    private readonly Redirector _redirector = new();
    private readonly ReturnHandler _returnHandler = new();
    private Expr? _lastExpr;

    public Interpreter()
    {
        ShellEnvironment = new ShellEnvironment();
        _scope = new GlobalScope();
    }

    public IRuntimeValue Interpret(List<Expr> ast, GlobalScope? scope = null)
    {
        if (scope != null)
            _scope = scope;

        IRuntimeValue lastResult = RuntimeNil.Value;
        foreach (var expr in ast)
        {
            try
            {
                lastResult = Next(expr);
            }
            catch (RuntimeException e)
            {
                var pos = _lastExpr?.Position ?? TextPos.Default;
                string message = $"[{pos.Line}:{pos.Column}] {e.Message}";
                if (pos.FilePath != null)
                    message = $"{pos.FilePath} {message}";
                
                if (!PrintErrors)
                    throw new AggregateException(message, e);
                
                Console.Error.WriteLine(message);
                
                // Make sure the redirector is emptied
                _redirector.Receive();

                return RuntimeNil.Value;
            }
        }

        return lastResult;
    }

    public IRuntimeValue Interpret(string input, string? filePath)
    {
        try
        {
            var ast = Parser.Parse(
                Lexer.Lex(input, filePath),
                _scope.GlobalScope,
                filePath ?? ShellEnvironment.WorkingDirectory
            );

            return Interpret(ast);
        }
        catch (ParseException e)
        {
            string message = $"[{e.Position.Line}:{e.Position.Column}] {e.Message}";
            if (e.Position.FilePath != null)
                message = $"{e.Position.FilePath} {message}";
            
            if (!PrintErrors)
                throw new AggregateException(message, e);
                
            Console.Error.WriteLine(message);

            return RuntimeNil.Value;
        }
    }

    public bool FunctionExists(string name)
        => _scope.GlobalScope.ContainsFunction(name);

    public bool VariableExists(string name)
        => _scope.GlobalScope.ContainsVariable(name);

    public void AddGlobalVariable(string name, IRuntimeValue value)
        => _scope.GlobalScope.AddVariable(name, value);

    private IRuntimeValue Next(Expr expr)
    {
        if (_returnHandler.Active)
            return RuntimeNil.Value;

        _lastExpr = expr;

        return expr switch
        {
            FunctionExpr => RuntimeNil.Value,
            LetExpr e => Visit(e),
            KeywordExpr e => Visit(e),
            IfExpr e => Visit(e),
            ForExpr e => Visit(e),
            WhileExpr e => Visit(e),
            TupleExpr e => Visit(e),
            ListExpr e => Visit(e),
            DictionaryExpr e => Visit(e),
            BlockExpr e => Visit(e),
            LiteralExpr e => Visit(e),
            StringInterpolationExpr e => Visit(e),
            BinaryExpr e => Visit(e),
            UnaryExpr e => Visit(e),
            RangeExpr e => Visit(e),
            IndexerExpr e => Visit(e),
            VariableExpr e => Visit(e),
            CallExpr e => Visit(e),
            _ => throw new ArgumentOutOfRangeException(nameof(expr), expr, null),
        };
    }

    private IRuntimeValue Visit(LetExpr expr)
    {
        SetVariables(expr.IdentifierList, Next(expr.Value));
        
        return RuntimeNil.Value;
    }

    private void SetVariables(List<Token> identifiers, IRuntimeValue value, Scope.Scope? scope = null)
    {
        if (identifiers.Count > 1)
        {
            if (value is not IEnumerable<IRuntimeValue> values)
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

    private void SetVariable(string name, IRuntimeValue value, Scope.Scope scope)
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

    private IRuntimeValue Visit(KeywordExpr expr)
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

    private IRuntimeValue Visit(IfExpr expr)
    {
        var condition = Next(expr.Condition);
        var conditionValue = condition.As<RuntimeBoolean>();

        expr.ThenBranch.IsRoot = expr.IsRoot;
        if (expr.ElseBranch != null)
            expr.ElseBranch.IsRoot = expr.IsRoot;

        if (conditionValue.Value)
        {
            return Next(expr.ThenBranch);
        }
        
        return expr.ElseBranch == null
            ? RuntimeNil.Value
            : Next(expr.ElseBranch);
    }

    private IRuntimeValue Visit(ForExpr expr)
    {
        var value = Next(expr.Value);

        if (value is not IEnumerable<IRuntimeValue> enumerableValue)
            throw new RuntimeIterationException(value.GetType());

        expr.Branch.IsRoot = true;

        var scope = new LocalScope(_scope);
        foreach (var current in enumerableValue)
        {
            SetVariables(expr.IdentifierList, current, scope);
            Visit(expr.Branch, scope);
            scope.Clear();

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

    private IRuntimeValue Visit(WhileExpr expr)
    {
        expr.Branch.IsRoot = true;

        while (Next(expr.Condition).As<RuntimeBoolean>().Value)
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

    private IRuntimeValue Visit(TupleExpr expr)
    {
        var values = expr.Values.Select(Next).ToList();

        return new RuntimeTuple(values);
    }

    private IRuntimeValue Visit(ListExpr expr)
    {
        return new RuntimeList(expr.Values.Select(Next).ToList());
    }

    private IRuntimeValue Visit(DictionaryExpr expr)
    {
        var dict = new Dictionary<int, (IRuntimeValue, IRuntimeValue)>();
        foreach (var entry in expr.Entries)
        {
            var key = new RuntimeString(entry.Item1);
            dict.Add(key.GetHashCode(), (key, Next(entry.Item2)));
        }

        return new RuntimeDictionary(dict);
    }

    private IRuntimeValue Visit(BlockExpr expr, LocalScope? scope = null)
    {
        _scope = scope ?? new LocalScope(_scope);

        int i = 0;
        IRuntimeValue lastValue = RuntimeNil.Value;
        foreach (var child in expr.Expressions)
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

            child.IsRoot = true;
            Next(child);
            i++;
        }

        _scope = _scope.Parent!;

        BlockShouldExit(expr, out var explicitReturnValue);

        return explicitReturnValue ?? lastValue;
    }

    private bool BlockShouldExit(BlockExpr expr, out IRuntimeValue? returnValue)
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

    private IRuntimeValue Visit(LiteralExpr expr)
    {
        return expr.Value.Kind switch
        {
            TokenKind.IntegerLiteral => new RuntimeInteger(((IntegerLiteralExpr)expr).NumberValue),
            TokenKind.FloatLiteral => new RuntimeFloat(((FloatLiteralExpr)expr).NumberValue),
            TokenKind.StringLiteral => new RuntimeString(expr.Value.Value),
            TokenKind.True => RuntimeBoolean.True,
            TokenKind.False => RuntimeBoolean.False,
            TokenKind.Nil => RuntimeNil.Value,
            _ => throw new ArgumentOutOfRangeException(),
        };
    }

    private IRuntimeValue Visit(StringInterpolationExpr expr)
    {
        var result = new StringBuilder();
        foreach (var part in expr.Parts)
        {
            result.Append(Next(part).As<RuntimeString>().Value);
        }

        return new RuntimeString(result.ToString());
    }

    private IRuntimeValue Visit(BinaryExpr expr)
    {
        if (expr.Operator == OperationKind.Pipe)
        {
            _redirector.Open();
            _redirector.Send(Next(expr.Left));
            var result = Next(expr.Right);

            return result;
        }
        
        if (expr.Operator == OperationKind.Equals)
        {
            return EvaluateAssignment(expr.Left, Next(expr.Right));
        }
        
        if (expr.Operator == OperationKind.If)
        {
            return Next(expr.Right).As<RuntimeBoolean>().Value
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

        var left = Next(expr.Left);
        if (expr.Operator == OperationKind.Coalescing)
        {
            return left is RuntimeNil
                ? Next(expr.Right)
                : left;
        }

        if (expr.Operator == OperationKind.And)
        {
            bool result = left.As<RuntimeBoolean>().Value &&
                Next(expr.Right).As<RuntimeBoolean>().Value;

            return RuntimeBoolean.From(result);
        }

        if (expr.Operator == OperationKind.Or)
        {
            bool result = left.As<RuntimeBoolean>().Value ||
                Next(expr.Right).As<RuntimeBoolean>().Value;

            return RuntimeBoolean.From(result);
        }

        var right = Next(expr.Right);
        if (expr.Operator == OperationKind.In)
        {
            bool result = right switch
            {
                RuntimeList list => list.Values
                    .Find(x => x.Operation(OperationKind.EqualsEquals, left).As<RuntimeBoolean>().Value) != null,
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

    private IRuntimeValue EvaluateAssignment(Expr assignee, IRuntimeValue value)
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
            if (Next(indexer.Value) is not IIndexable<IRuntimeValue> indexable)
                throw new RuntimeUnableToIndexException(value.GetType());

            indexable[Next(indexer.Index)] = value;
        }
        else
        {
            throw new RuntimeException("Invalid assignment");
        }

        return value;
    }

    private IRuntimeValue Visit(UnaryExpr expr)
    {
        var value = Next(expr.Value);

        if (expr.Operator == OperationKind.Not)
            value = value.As<RuntimeBoolean>();

        return value.Operation(expr.Operator);
    }

    private IRuntimeValue Visit(RangeExpr expr)
    {
        int? from = expr.From == null
            ? null
            : Next(expr.From).As<RuntimeInteger>().Value;
        int? to = expr.To == null
            ? null
            : Next(expr.To).As<RuntimeInteger>().Value;
        
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

    private IRuntimeValue Visit(IndexerExpr expr)
    {
        var value = Next(expr.Value);
        if (value is IIndexable<IRuntimeValue> indexableValue)
        {
            return indexableValue[Next(expr.Index)];
        }

        throw new RuntimeUnableToIndexException(value.GetType());
    }

    private IRuntimeValue Visit(VariableExpr expr)
    {
        string name = expr.Identifier.Value;
        if (name.StartsWith('$'))
        {
            string? value = Environment.GetEnvironmentVariable(name[1..]);

            return value == null
                ? RuntimeNil.Value
                : new RuntimeString(value);
        }

        return _scope.FindVariable(name) ?? RuntimeNil.Value;
    }

    private IRuntimeValue Visit(CallExpr expr)
    {
        string name = expr.Identifier.Value;
        if (name == "cd")
            return EvaluateCd(expr.Arguments);
        if (name == "scriptPath")
            return EvaluateScriptPath(expr.Arguments);

        if (StdGateway.Contains(name))
        {
            var arguments = new List<object?>(expr.Arguments.Count + 1);
            if (_redirector.Status == RedirectorStatus.HasData)
            {
                arguments.Add(_redirector.Receive() ?? RuntimeNil.Value);
            }

            foreach (var argument in expr.Arguments)
                arguments.Add(Next(argument));


            return StdGateway.Call(name, arguments, ShellEnvironment);
        }

        var function = _scope.GlobalScope.FindFunction(name);

        return function == null
            ? EvaluateProgramCall(expr)
            : EvaluateFunctionCall(expr, function);
    }

    private IRuntimeValue EvaluateCd(List<Expr> argumentExpressions)
    {
        if (argumentExpressions.Count > 1)
            throw new RuntimeWrongNumberOfArgumentsException(1, argumentExpressions.Count);


        string argument = argumentExpressions.Any()
            ? Next(argumentExpressions.First()).As<RuntimeString>().Value
            : "";
        if (argument == "")
        {
            ShellEnvironment.WorkingDirectory = "";
            return RuntimeNil.Value;
        }

        string path = argument == "-"
            ? Environment.GetEnvironmentVariable("OLDPWD") ?? ""
            : ShellEnvironment.GetAbsolutePath(argument);

        if (Directory.Exists(path))
        {
            ShellEnvironment.WorkingDirectory = path;
        }
        else
        {
            return new RuntimeError($"cd: The directory \"{path}\" does not exist");
        }

        return RuntimeNil.Value;
    }

    private IRuntimeValue EvaluateScriptPath(List<Expr> arguments)
    {
        if (arguments.Any())
            throw new RuntimeWrongNumberOfArgumentsException(0, arguments.Count);

        string path = _lastExpr!.Position.FilePath == null
            ? ShellEnvironment.WorkingDirectory
            : Path.GetDirectoryName(_lastExpr.Position.FilePath)!;

        return new RuntimeString(path);
    }

    private IRuntimeValue EvaluateFunctionCall(CallExpr call, FunctionExpr function)
    {
        var functionScope = new LocalScope(_scope);
        bool encounteredDefaultParameter = false;
        RuntimeList? variadicArguments = null;
        foreach (var (parameter, argument) in function.Parameters.ZipLongest(call.Arguments))
        {
            if (parameter?.DefaultValue != null)
                encounteredDefaultParameter = true;

            if (parameter == null && variadicArguments == null ||
                argument == null && parameter?.DefaultValue == null && parameter?.Variadic is false)
            {
                bool variadic = function.Parameters.LastOrDefault()?.Variadic is true;
                throw new RuntimeWrongNumberOfArgumentsException(function.Parameters.Count, call.Arguments.Count, variadic);
            }

            if (encounteredDefaultParameter && parameter?.DefaultValue == null)
            {
                throw new RuntimeException("Optional parameters may only occur at the end of parameter lists");
            }

            if (variadicArguments != null && parameter != null)
            {
                throw new RuntimeException("Variadic parameters may only occur at the end of parameter lists");
            }
            
            if (parameter?.Variadic is true)
            {
                variadicArguments = new RuntimeList(new List<IRuntimeValue>());
                functionScope.AddVariable(parameter.Identifier.Value, variadicArguments);
            }

            if (variadicArguments != null)
            {
                if (argument != null)
                    variadicArguments.Values.Add(Next(argument));
                continue;
            }
            
            functionScope.AddVariable(
                parameter!.Identifier.Value,
                argument == null ? Next(parameter.DefaultValue!) : Next(argument)
            );
        }

        function.Block.IsRoot = call.IsRoot;

        return Visit(function.Block, functionScope);
    }

    private IRuntimeValue EvaluateProgramCall(CallExpr expr)
    {
        var arguments = new List<string>();
        foreach (var argumentExpr in expr.Arguments)
        {
            var argument = Next(argumentExpr);
            string value = argument is RuntimeNil
                ? string.Empty
                : argument.As<RuntimeString>().Value;
            if (expr.CallStyle == CallStyle.TextArguments)
            {
                var matcher = new Matcher();
                matcher.AddInclude(value);
                var result = matcher.Execute(
                    new DirectoryInfoWrapper(new DirectoryInfo(ShellEnvironment.WorkingDirectory))
                );

                if (result.HasMatches)
                {
                    arguments.AddRange(result.Files.Select(x => x.Path));
                    continue;
                }
            }

            arguments.Add(value);
        }

        bool stealOutput = _redirector.Status == RedirectorStatus.ExpectingInput || !expr.IsRoot;

        // Read potential shebang
        string fileName = expr.Identifier.Value;
        bool hasShebang = false;
        if (File.Exists(ShellEnvironment.GetAbsolutePath(fileName)))
        {
            using var streamReader = new StreamReader(ShellEnvironment.GetAbsolutePath(fileName));
            var firstChars = new char[2];
            streamReader.ReadBlock(firstChars, 0, 2);

            if (firstChars[0] == '#' && firstChars[1] == '!')
            {
                arguments.Insert(0, fileName);
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
                Arguments = string.Join(" ", arguments.Select(EscapeArgument)),
                RedirectStandardOutput = stealOutput,
                RedirectStandardError = stealOutput,
                RedirectStandardInput = _redirector.Status == RedirectorStatus.HasData,
                WorkingDirectory = ShellEnvironment.WorkingDirectory,
            },
        };

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new RuntimeNotFoundException(expr.Identifier.Value);
        }

        if (_redirector.Status == RedirectorStatus.HasData)
        {
            try
            {
                using var streamWriter = process.StandardInput;
                streamWriter.Write(_redirector.Receive());
            }
            catch (IOException e)
            {
                throw new RuntimeException(e.Message);
            }
        }

        process.WaitForExit();

        if (stealOutput)
        {
            return process.ExitCode == 0
                ? new RuntimeString(process.StandardOutput.ReadToEnd())
                : new RuntimeError(process.StandardError.ReadToEnd());
        }
        
        return RuntimeNil.Value;
    }

    private static string EscapeArgument(string argument)
        => $"\"{argument.Replace("\"", "\"\"\"")}\"";
}