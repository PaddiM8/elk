using System;
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

    public Interpreter()
    {
        _scope = new GlobalScope();
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
        => Interpret(Parser.Parse(Lexer.Lex(input), _scope.GlobalScope));

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
            BlockExpr e => Visit(e),
            LiteralExpr e => Visit(e),
            BinaryExpr e => Visit(e),
            UnaryExpr e => Visit(e),
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
        var conditionValue = (RuntimeBoolean)condition.Cast(RuntimeType.Boolean);

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
            TokenKind.NumberLiteral => new RuntimeNumber(double.Parse(expr.Value.Value)),
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

    private IRuntimeValue Visit(VariableExpr expr)
    {
        return _scope.FindVariable(expr.Identifier.Value) ?? RuntimeNil.Value;
    }

    private IRuntimeValue Visit(CallExpr expr)
    {
        var function = _scope.GlobalScope.FindFunction(expr.Identifier.Value);

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
        var arguments = expr.Arguments.Select(x => Next(x).ToString());
        bool stealOutput = _redirector.Status == RedirectorStatus.ExpectingInput || !expr.IsRoot;
        using var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = expr.Identifier.Value,
                Arguments = string.Join(" ", arguments),
                RedirectStandardOutput = stealOutput,
                RedirectStandardError = stealOutput,
                RedirectStandardInput = _redirector.Status == RedirectorStatus.HasData,
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