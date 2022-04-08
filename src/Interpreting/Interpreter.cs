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
    private IRuntimeValue? _functionReturnValue = null;
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
            }
        }

        return lastResult;
    }

    public IRuntimeValue Interpret(string input)
    {
        try
        {
            var ast = Parser.Parse(Lexer.Lex(input), _scope.GlobalScope);

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
        if (_functionReturnValue != null)
            return RuntimeNil.Value;

        _lastExpr = expr;

        return expr switch
        {
            FunctionExpr e => Visit(e),
            LetExpr e => Visit(e),
            ReturnExpr e => Visit(e),
            IfExpr e => Visit(e),
            ListExpr e => Visit(e),
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
        _scope.UpdateVariable(expr.Identifier.Value, Next(expr.Value));

        return RuntimeNil.Value;
    }

    private IRuntimeValue Visit(ReturnExpr expr)
    {
        _functionReturnValue = Next(expr.Value);

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

    private IRuntimeValue Visit(ListExpr expr)
    {
        var values = new List<IRuntimeValue>();
        foreach (var subExpr in expr.Values)
        {
            values.Add(Next(subExpr));
        }

        return new RuntimeList(values);
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
            if (_functionReturnValue != null)
            {
                lastValue = _functionReturnValue;
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

        var left = Next(expr.Left);
        var right = Next(expr.Right);

        return expr.Operator switch
        {
            TokenKind.Plus
            or TokenKind.Minus
            or TokenKind.Star
            or TokenKind.Slash
            or TokenKind.Greater
            or TokenKind.GreaterEquals
            or TokenKind.Less
            or TokenKind.LessEquals
            or TokenKind.EqualsEquals
            or TokenKind.NotEquals
            or TokenKind.And
            or TokenKind.Or => left.Operation(expr.Operator, right),
            _ => throw new NotImplementedException(),
        };
    }

    private IRuntimeValue Visit(UnaryExpr expr)
    {
        var value = Next(expr.Value);

        return expr.Operator switch
        {
            TokenKind.Minus or TokenKind.Exclamation => value.Operation(expr.Operator),
            _ => throw new NotImplementedException(),
        };
    }

    private IRuntimeValue Visit(RangeExpr expr)
    {
        int? from = expr.From == null
            ? null
            : Next(expr.From).As<RuntimeInteger>().Value;
        int? to = expr.To == null
            ? null
            : Next(expr.To).As<RuntimeInteger>().Value;

        return new RuntimeRange(from, to);
    }

    private IRuntimeValue Visit(IndexerExpr expr)
    {
        var value = Next(expr.Value);
        if (value is IIndexable<IRuntimeValue> indexableValue)
        {
            var index = Next(expr.Index).As<RuntimeInteger>().Value;

            return indexableValue[index];
        }

        throw new RuntimeUnableToIndexException(value.GetType());
    }

    private IRuntimeValue Visit(VariableExpr expr)
    {
        return _scope.FindVariable(expr.Identifier.Value) ?? RuntimeNil.Value;
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

        var returnValue = Visit(function.Block, functionScope);
        _functionReturnValue = null;

        return returnValue;
    }

    private IRuntimeValue EvaluateProgramCall(CallExpr expr)
    {
        var arguments = expr.Arguments.Select(x => Next(x).ToString()).ToList();
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
                Arguments = string.Join(" ", arguments),
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