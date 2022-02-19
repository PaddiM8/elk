using System;
using System.Diagnostics;
using System.Linq;
using Shel.Lexing;
using Shel.Parsing;

namespace Shel.Interpreting;

class Interpreter
{
    private Scope _scope;
    private Redirector _redirector = new();

    public Interpreter()
    {
        _scope = new Scope(null);
    }

    public IRuntimeValue Interpret(string input)
    {
        var ast = Parser.Parse(input, _scope);

        IRuntimeValue lastResult = new RuntimeNil();
        foreach (var expr in ast)
            lastResult = Next(expr);

        return lastResult;
    }

    private IRuntimeValue Next(Expr expr)
    {
        return expr switch
        {
            LetExpr e => Visit(e),
            LiteralExpr e => Visit(e),
            BinaryExpr e => Visit(e),
            UnaryExpr e => Visit(e),
            VariableExpr e => Visit(e),
            CallExpr e => Visit(e),
            _ => throw new NotImplementedException(),
        };
    }

    private IRuntimeValue Visit(LetExpr expr)
    {
        _scope.UpdateVariable(expr.Identifier.Value, Next(expr.Value));

        return new RuntimeNil();
    }

    private IRuntimeValue Visit(LiteralExpr expr)
    {
        return expr.Value.Kind switch
        {
            TokenKind.NumberLiteral => new RuntimeNumber(double.Parse(expr.Value.Value)),
            TokenKind.StringLiteral => new RuntimeString(expr.Value.Value),
            TokenKind.Nil => new RuntimeNil(),
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
            or TokenKind.NotEquals => left.Operation(expr.Operator, right),
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
        return _scope.FindVariable(expr.Identifier.Value) ?? new RuntimeNil();
    }

    private IRuntimeValue Visit(CallExpr expr)
    {
        var process = new Process()
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = expr.Identifier.Value,
                Arguments = string.Join(" ", expr.Arguments.Select(x => Next(x).ToString())),
                RedirectStandardOutput = _redirector.Status == RedirectorStatus.Send,
                RedirectStandardInput = _redirector.Status == RedirectorStatus.Receive,
            }
        };
        process.Start();

        if (_redirector.Status == RedirectorStatus.Receive)
        {
            using var streamWriter = process.StandardInput;
            streamWriter.Write(_redirector.Receive());
        }

        process.WaitForExit();

        if (_redirector.Status == RedirectorStatus.Send)
        {
            string output = process.ExitCode == 0
                ? process.StandardOutput.ReadToEnd()
                : process.StandardError.ReadToEnd();

            return new RuntimeString(output);
        }
        else
        {
            return new RuntimeNil();
        }
    }
}