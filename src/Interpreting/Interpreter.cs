using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Shel.Lexing;
using Shel.Parsing;

namespace Shel.Interpreting;

class Interpreter
{
    private Scope _scope;
    private readonly Redirector _redirector = new();
    private readonly ReturnationHandler _returnationHandler = new();
    private Expr? _lastExpr = null;

    public ShellEnvironment ShellEnvironment { get; }

    public Interpreter()
    {
        _scope = new GlobalScope();
        ShellEnvironment = new ShellEnvironment();
    }

    public IRuntimeValue Interpret(List<Expr> ast)
    {
        IRuntimeValue lastResult = RuntimeNil.Value;
        foreach (var expr in ast)
        {
            try
            {
                lastResult = Next(expr);
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

    public IRuntimeValue Interpret(string input, string? filePath)
    {
        try
        {
            var ast = Parser.Parse(
                Lexer.Lex(input),
                _scope.GlobalScope,
                filePath ?? ShellEnvironment.WorkingDirectory
            );

            return Interpret(ast);
        }
        catch (ParseException e)
        {
            Console.WriteLine($"[{e.Position.Line}:{e.Position.Column}] {e.Message}");

            return RuntimeNil.Value;
        }
    }

    private IRuntimeValue Next(Expr expr)
    {
        if (_returnationHandler.Active)
            return RuntimeNil.Value;

        _lastExpr = expr;

        return expr switch
        {
            FunctionExpr e => Visit(e),
            LetExpr e => Visit(e),
            KeywordExpr e => Visit(e),
            IfExpr e => Visit(e),
            ForExpr e => Visit(e),
            TupleExpr e => Visit(e),
            ListExpr e => Visit(e),
            DictionaryExpr e => Visit(e),
            BlockExpr e => Visit(e),
            LiteralExpr e => Visit(e),
            BinaryExpr e => Visit(e),
            UnaryExpr e => Visit(e),
            RangeExpr e => Visit(e),
            IndexerExpr e => Visit(e),
            VariableExpr e => Visit(e),
            CallExpr e => Visit(e),
            _ => throw new NotImplementedException(),
        };
    }

    private IRuntimeValue Visit(FunctionExpr _)
    {
        return RuntimeNil.Value;
    }

    private IRuntimeValue Visit(LetExpr expr)
    {
        string name = expr.Identifier.Value;
        if (name.StartsWith('$'))
        {
            Environment.SetEnvironmentVariable(
                name[1..],
                Next(expr.Value).As<RuntimeString>().Value
            );
        }
        else
        {
            _scope.UpdateVariable(expr.Identifier.Value, Next(expr.Value));
        }

        return RuntimeNil.Value;
    }

    private IRuntimeValue Visit(KeywordExpr expr)
    {
        var returnationType = expr.Kind switch
        {
            TokenKind.Break => ReturnationType.BreakLoop,
            TokenKind.Continue => ReturnationType.ContinueLoop,
            TokenKind.Return => ReturnationType.ReturnFunction,
            _ => throw new NotImplementedException(),
        };
        var value = expr.Value == null
            ? RuntimeNil.Value
            : Next(expr.Value);

        _returnationHandler.TriggerReturn(returnationType, value);

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
        else
        {
            return expr.ElseBranch == null
                ? RuntimeNil.Value
                : Next(expr.ElseBranch);
        }
    }

    private IRuntimeValue Visit(ForExpr expr)
    {
        var value = Next(expr.Value);

        if (value is not IEnumerable<IRuntimeValue> enumerableValue)
            throw new RuntimeIterationException(value.GetType());

        expr.Branch.IsRoot = expr.IsRoot;

        var enumerator = enumerableValue.GetEnumerator();
        var scope = new LocalScope(_scope);
        while (enumerator.MoveNext())
        {
            scope.AddVariable(expr.Identifier.Value, enumerator.Current);
            Visit(expr.Branch, scope);
            scope.Clear();

            if (_returnationHandler.ReturnationType == ReturnationType.BreakLoop)
            {
                return _returnationHandler.Collect();
            }

            if (_returnationHandler.ReturnationType == ReturnationType.ContinueLoop)
            {
                _returnationHandler.Collect();
            }
        }

        return RuntimeNil.Value;
    }

    private IRuntimeValue Visit(TupleExpr expr)
    {
        var values = new List<IRuntimeValue>();
        foreach (var subExpr in expr.Values)
        {
            values.Add(Next(subExpr));
        }

        return new RuntimeTuple(values);
    }

    private IRuntimeValue Visit(ListExpr expr)
    {
        var values = new List<IRuntimeValue>();
        foreach (var subExpr in expr.Values)
        {
            values.Add(Next(subExpr));
        }

        return new RuntimeList(values);
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
            if (_returnationHandler.Active)
            {
                if (expr.ParentStructureKind == StructureKind.Function &&
                    _returnationHandler.ReturnationType == ReturnationType.ReturnFunction)
                {
                    lastValue = _returnationHandler.Collect();
                }

                break;
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

        return lastValue;
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
            _ => throw new NotImplementedException(),
        };
    }

    private IRuntimeValue Visit(BinaryExpr expr)
    {
        if (expr.Operator == TokenKind.Pipe)
        {
            _redirector.Open();
            _redirector.Send(Next(expr.Left));
            var result = Next(expr.Right);

            return result;
        }


        if (expr.Operator == TokenKind.Equals)
        {
            return EvaluateAssignment(expr.Left, Next(expr.Right));
        }

        var left = Next(expr.Left);
        if (expr.Operator == TokenKind.QuestionQuestion)
        {
            return left is RuntimeNil
                ? Next(expr.Right)
                : left;
        }

        var right = Next(expr.Right);

        return expr.Operator switch
        {
            TokenKind.Percent => left.As<RuntimeInteger>().Operation(expr.Operator, right.As<RuntimeInteger>()),
            _ => left.Operation(expr.Operator, right)
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
        {
            var arguments = expr.Arguments.Select(x => Next(x).As<RuntimeString>().Value);
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
                arguments.Add(Next(argument));


            return StdGateway.Call(name, arguments, ShellEnvironment);
        }

        var function = _scope.GlobalScope.FindFunction(name);

        return function == null
            ? EvaluateProgramCall(expr)
            : EvaluateFunctionCall(expr, function);
    }

    private IRuntimeValue EvaluateFunctionCall(CallExpr call, FunctionExpr function)
    {
        if (call.Arguments.Count != function.Parameters.Count)
        {
            throw new RuntimeException($"Expected {function.Parameters.Count} arguments but got {call.Arguments.Count}");
        }

        var functionScope = new LocalScope(_scope);
        foreach (var (parameter, argument) in function.Parameters.Zip(call.Arguments))
        {
            functionScope.AddVariable(parameter.Value, Next(argument));
        }

        function.Block.IsRoot = call.IsRoot;

        return Visit(function.Block, functionScope);
    }

    private IRuntimeValue EvaluateProgramCall(CallExpr expr)
    {
        var arguments = expr.Arguments
            .Select(x => Next(x).As<RuntimeString>().Value)
            .ToList();
        bool stealOutput = _redirector.Status == RedirectorStatus.ExpectingInput || !expr.IsRoot;

        // Read potential shebang
        string fileName = expr.Identifier.Value;
        if (File.Exists(ShellEnvironment.GetAbsolutePath(fileName)))
        {
            using var streamReader = new StreamReader(ShellEnvironment.GetAbsolutePath(fileName));
            var firstChars = new char[2];
            streamReader.ReadBlock(firstChars, 0, 2);

            if (firstChars[0] == '#' && firstChars[1] == '!')
            {
                arguments.Insert(0, fileName);
                fileName = streamReader.ReadLine() ?? "";
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
            using var streamWriter = process.StandardInput;
            streamWriter.Write(_redirector.Receive());
        }

        process.WaitForExit();

        if (stealOutput)
        {
            string output = process.ExitCode == 0
                ? process.StandardOutput.ReadToEnd()
                : process.StandardError.ReadToEnd();

            return new RuntimeString(output);
        }
        else
        {
            return RuntimeNil.Value;
        }
    }
}