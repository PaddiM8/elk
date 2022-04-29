using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Elk.Lexing;
using Elk.Parsing;

namespace Elk.Interpreting;

class Interpreter
{
    private Scope.Scope _scope;
    private readonly Redirector _redirector = new();
    private readonly ReturnHandler _returnHandler = new();
    private Expr? _lastExpr;

    public ShellEnvironment ShellEnvironment { get; }

    public Interpreter(GlobalScope? scope = null)
    {
        _scope = scope ?? new GlobalScope();
        ShellEnvironment = new ShellEnvironment();
    }

    public async Task<IRuntimeValue> Interpret(List<Expr> ast)
    {
        IRuntimeValue lastResult = RuntimeNil.Value;
        foreach (var expr in ast)
        {
            try
            {
                lastResult = await Next(expr);
            }
            catch (RuntimeException e)
            {
                var pos = _lastExpr?.Position ?? new TextPos(0, 0);
                Console.WriteLine($"[{pos.Line}:{pos.Column}] {e.Message}");

                return RuntimeNil.Value;
            }
        }

        return lastResult;
    }

    public async Task<IRuntimeValue> Interpret(string input, string? filePath)
    {
        try
        {
            var ast = Parser.Parse(
                Lexer.Lex(input),
                _scope.GlobalScope,
                filePath ?? ShellEnvironment.WorkingDirectory
            );

            return await Interpret(ast);
        }
        catch (ParseException e)
        {
            Console.WriteLine($"[{e.Position.Line}:{e.Position.Column}] {e.Message}");

            return RuntimeNil.Value;
        }
    }

    public static async Task InterpretBlock(BlockExpr block, LocalScope scope, PauseToken<IRuntimeValue> pauseToken)
    {
        var interpreter = new Interpreter(scope.GlobalScope);
        await interpreter.Visit(block, scope, pauseToken);
    }

    private async Task<IRuntimeValue> Next(Expr expr)
    {
        if (_returnHandler.Active)
            return RuntimeNil.Value;

        _lastExpr = expr;

        return expr switch
        {
            FunctionExpr _ => Visit(),
            LetExpr e => await Visit(e),
            KeywordExpr e => await Visit(e),
            IfExpr e => await Visit(e),
            ForExpr e => await Visit(e),
            TupleExpr e => await Visit(e),
            ListExpr e => await Visit(e),
            DictionaryExpr e => await Visit(e),
            BlockExpr e => await Visit(e),
            LiteralExpr e => Visit(e),
            BinaryExpr e => await Visit(e),
            UnaryExpr e => await Visit(e),
            RangeExpr e => await Visit(e),
            IndexerExpr e => await Visit(e),
            VariableExpr e => Visit(e),
            CallExpr e => await Visit(e),
            _ => throw new ArgumentOutOfRangeException(nameof(expr), expr, null),
        };
    }

    private IRuntimeValue Visit()
    {
        return RuntimeNil.Value;
    }

    private async Task<IRuntimeValue> Visit(LetExpr expr)
    {
        string name = expr.Identifier.Value;
        if (name.StartsWith('$'))
        {
            Environment.SetEnvironmentVariable(
                name[1..],
                (await Next(expr.Value)).As<RuntimeString>().Value
            );
        }
        else
        {
            _scope.AddVariable(expr.Identifier.Value, await Next(expr.Value));
        }

        return RuntimeNil.Value;
    }

    private async Task<IRuntimeValue> Visit(KeywordExpr expr)
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
            : await Next(expr.Value);

        _returnHandler.TriggerReturn(returnKind, value);

        return RuntimeNil.Value;
    }

    private async Task<IRuntimeValue> Visit(IfExpr expr)
    {
        var condition = await Next(expr.Condition);
        var conditionValue = condition.As<RuntimeBoolean>();

        expr.ThenBranch.IsRoot = expr.IsRoot;
        if (expr.ElseBranch != null)
            expr.ElseBranch.IsRoot = expr.IsRoot;

        if (conditionValue.Value)
        {
            return await Next(expr.ThenBranch);
        }
        else
        {
            return expr.ElseBranch == null
                ? RuntimeNil.Value
                : await Next(expr.ElseBranch);
        }
    }

    private async Task<IRuntimeValue> Visit(ForExpr expr)
    {
        var value = await Next(expr.Value);
        expr.Branch.IsRoot = expr.IsRoot;

        var scope = new LocalScope(_scope);
        if (value is IEnumerable<IRuntimeValue> enumerableValue)
        {
            foreach (var item in enumerableValue)
            {
                var result = await EvaluateLoopIteration(expr, scope, item);
                if (result != null)
                    return result;
            }
        }
        else if (value is IAsyncEnumerable<IRuntimeValue> asyncEnumerableValue)
        {
            await foreach (var item in asyncEnumerableValue)
            {
                var result = await EvaluateLoopIteration(expr, scope, item);
                if (result != null)
                    return result;
            }
        }
        else
        {
            throw new RuntimeIterationException(value.GetType());
        }

        return RuntimeNil.Value;
    }

    private async Task<IRuntimeValue?> EvaluateLoopIteration(ForExpr forExpr, LocalScope scope, IRuntimeValue current)
    {
        scope.AddVariable(forExpr.Identifier.Value, current);
        await Visit(forExpr.Branch, scope);
        scope.Clear();

        if (_returnHandler.ReturnKind == ReturnKind.BreakLoop)
        {
            return _returnHandler.Collect();
        }

        if (_returnHandler.ReturnKind == ReturnKind.ContinueLoop)
        {
            _returnHandler.Collect();
        }

        return null;
    }

    private async Task<IRuntimeValue> Visit(TupleExpr expr)
    {
        var values = await expr.Values
            .Select(async x => await Next(x))
            .WhenAll();

        return new RuntimeTuple(values);
    }

    private async Task<IRuntimeValue> Visit(ListExpr expr)
    {
        var values = await expr.Values
            .Select(async x => await Next(x))
            .WhenAll();
        
        return new RuntimeList(values);
    }

    private async Task<IRuntimeValue> Visit(DictionaryExpr expr)
    {
        var dict = new Dictionary<int, (IRuntimeValue, IRuntimeValue)>();
        foreach (var entry in expr.Entries)
        {
            var key = new RuntimeString(entry.Item1);
            dict.Add(key.GetHashCode(), (key, await Next(entry.Item2)));
        }

        return new RuntimeDictionary(dict);
    }

    private async Task<IRuntimeValue> Visit(BlockExpr expr, LocalScope? scope = null, PauseToken<IRuntimeValue>? pauseToken = null)
    {
        _scope = scope ?? new LocalScope(_scope);

        int i = 0;
        IRuntimeValue lastValue = RuntimeNil.Value;
        foreach (var child in expr.Expressions)
        {
            if (child is KeywordExpr { Kind: TokenKind.Yield } keywordExpr)
            {
                var yieldValue = keywordExpr.Value == null
                    ? RuntimeNil.Value
                    : await Next(keywordExpr.Value);
                await pauseToken!.Value.PauseIfRequestedAsync(yieldValue);

                continue;
            }
            
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
                lastValue = await Next(child);
                break;
            }

            child.IsRoot = true;
            await Next(child);
            i++;
        }

        _scope = _scope.Parent!;

        BlockShouldExit(expr, out var explicitReturnValue);

        if (pauseToken != null)
            await pauseToken.Value.FinishAsync();

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

    private async Task<IRuntimeValue> Visit(BinaryExpr expr)
    {
        if (expr.Operator == TokenKind.Pipe)
        {
            _redirector.Open();
            _redirector.Send(await Next(expr.Left));
            var result = await Next(expr.Right);

            return result;
        }
        
        if (expr.Operator == TokenKind.Equals)
        {
            return await EvaluateAssignment(expr.Left, await Next(expr.Right));
        }
        
        if (expr.Operator == TokenKind.If)
        {
            return (await Next(expr.Right)).As<RuntimeBoolean>().Value
                ? await Next(expr.Left)
                : RuntimeNil.Value;
        }

        var left = await Next(expr.Left);
        if (expr.Operator == TokenKind.QuestionQuestion)
        {
            return left is RuntimeNil
                ? await Next(expr.Right)
                : left;
        }

        var right = await Next(expr.Right);

        return expr.Operator switch
        {
            TokenKind.Percent => left.As<RuntimeInteger>().Operation(expr.Operator, right.As<RuntimeInteger>()),
            _ => left.Operation(expr.Operator, right),
        };
    }

    private async Task<IRuntimeValue> EvaluateAssignment(Expr assignee, IRuntimeValue value)
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
            if (await Next(indexer.Value) is not IIndexable<IRuntimeValue> indexable)
                throw new RuntimeUnableToIndexException(value.GetType());

            indexable[await Next(indexer.Index)] = value;
        }
        else
        {
            throw new RuntimeException("Invalid assignment");
        }

        return value;
    }

    private async Task<IRuntimeValue> Visit(UnaryExpr expr)
    {
        var value = await Next(expr.Value);

        return value.Operation(expr.Operator);
    }

    private async Task<IRuntimeValue> Visit(RangeExpr expr)
    {
        int? from = expr.From == null
            ? null
            : (await Next(expr.From)).As<RuntimeInteger>().Value;
        int? to = expr.To == null
            ? null
            : (await Next(expr.To)).As<RuntimeInteger>().Value;
        
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

    private async Task<IRuntimeValue> Visit(IndexerExpr expr)
    {
        var value = await Next(expr.Value);
        if (value is IIndexable<IRuntimeValue> indexableValue)
        {
            return indexableValue[await Next(expr.Index)];
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

    private async Task<IRuntimeValue> Visit(CallExpr expr)
    {
        string name = expr.Identifier.Value;
        if (name == "cd")
        {
            var arguments = await expr.Arguments
                .Select(async x => (await Next(x)).As<RuntimeString>().Value)
                .WhenAll();
            string path = arguments.Any()
                ? string.Join(" ", arguments)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            ShellEnvironment.WorkingDirectory = ShellEnvironment.GetAbsolutePath(path);

            return RuntimeNil.Value;
        }
        
        if (StdGateway.Contains(name))
        {
            var arguments = new List<object?>(expr.Arguments.Count + 1);
            if (_redirector.Status == RedirectorStatus.HasData)
            {
                arguments.Add(_redirector.Receive() ?? RuntimeNil.Value);
            }

            foreach (var argument in expr.Arguments)
                arguments.Add(await Next(argument));


            return StdGateway.Call(name, arguments, ShellEnvironment);
        }

        var function = _scope.GlobalScope.FindFunction(name);

        return function == null
            ? await EvaluateProgramCall(expr)
            : await EvaluateFunctionCall(expr, function);
    }

    private async Task<IRuntimeValue> EvaluateFunctionCall(CallExpr call, FunctionExpr function)
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
                    variadicArguments.Values.Add(await Next(argument));
                continue;
            }
            
            functionScope.AddVariable(
                parameter!.Identifier.Value,
                argument == null ? await Next(parameter.DefaultValue!) : await Next(argument)
            );
        }

        if (function.HasYield)
        {
            return new RuntimeGenerator(function.Block, functionScope);
        }
        
        function.Block.IsRoot = call.IsRoot;

        return await Visit(function.Block, functionScope);
    }

    private async Task<IRuntimeValue> EvaluateProgramCall(CallExpr expr)
    {
        var arguments = new List<string>();
        foreach (var argumentExpr in expr.Arguments)
        {
            var argument = await Next(argumentExpr);
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
        if (File.Exists(ShellEnvironment.GetAbsolutePath(fileName)))
        {
            using var streamReader = new StreamReader(ShellEnvironment.GetAbsolutePath(fileName));
            var firstChars = new char[2];
            await streamReader.ReadBlockAsync(firstChars, 0, 2);

            if (firstChars[0] == '#' && firstChars[1] == '!')
            {
                arguments.Insert(0, fileName);
                fileName = await streamReader.ReadLineAsync() ?? "";
            }
        }

        using var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = string.Join(" ", arguments).Replace("\"", "\"\"\""),
                RedirectStandardOutput = stealOutput,
                RedirectStandardError = stealOutput,
                RedirectStandardInput = _redirector.Status == RedirectorStatus.HasData,
                WorkingDirectory = ShellEnvironment.WorkingDirectory,
            }
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
            await using var streamWriter = process.StandardInput;
            streamWriter.Write(_redirector.Receive());
        }

        await process.WaitForExitAsync();

        if (stealOutput)
        {
            string output = process.ExitCode == 0
                ? await process.StandardOutput.ReadToEndAsync()
                : await process.StandardError.ReadToEndAsync();

            return new RuntimeString(output);
        }
        
        return RuntimeNil.Value;
    }
}