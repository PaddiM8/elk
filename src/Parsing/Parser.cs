using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shel.Interpreting;
using Shel.Lexing;

namespace Shel.Parsing;

class Parser
{
    private readonly List<Token> _tokens;
    private int _index;
    private Scope _scope;

    private Token? Current => _index < _tokens.Count
        ? _tokens[_index]
        : null;

    private bool ReachedEnd => _index >= _tokens.Count;

    private Parser(List<Token> tokens, Scope scope)
    {
        _tokens = tokens;
        _scope = scope;
    }

    public static List<Expr> Parse(string input, Scope scope)
    {
        var parser = new Parser(Lexer.Lex(input), scope);
        var expressions = new List<Expr>();
        while (!parser.ReachedEnd)
        {
            expressions.Add(parser.ParseExpr());
        }

        return expressions;
    }

    private Expr ParseExpr()
    {
        if (AdvanceIf(TokenKind.Let))
        {
            return ParseLet();
        }

        return ParsePipe();
    }

    private Expr ParseLet()
    {
        var identifier = EatExpected(TokenKind.Identifier);
        EatExpected(TokenKind.Equals);
        _scope.AddVariable(identifier.Value, new RuntimeNil());

        return new LetExpr(identifier, ParseExpr());
    }

    private Expr ParsePipe()
    {
        var left = ParseOr();

        while (Match(TokenKind.Pipe))
        {
            var op = Eat().Kind;
            var right = ParseOr();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseOr()
    {
        var left = ParseAnd();

        while (Match(TokenKind.Or))
        {
            var op = Eat().Kind;
            var right = ParseAnd();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseAnd()
    {
        var left = ParseComparison();

        while (Match(TokenKind.And))
        {
            var op = Eat().Kind;
            var right = ParseComparison();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseComparison()
    {
        var left = ParseTerm();

        while (Match(
            TokenKind.Greater, 
            TokenKind.GreaterEquals,
            TokenKind.Less, 
            TokenKind.LessEquals,
            TokenKind.EqualsEquals,
            TokenKind.NotEquals))
        {
            var op = Eat().Kind;
            var right = ParseTerm();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseTerm()
    {
        var left = ParseFactor();

        while (Match(TokenKind.Plus, TokenKind.Minus))
        {
            var op = Eat().Kind;
            var right = ParseFactor();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseFactor()
    {
        var left = ParseUnary();

        while (Match(TokenKind.Star, TokenKind.Slash))
        {
            var op = Eat().Kind;
            var right = ParseUnary();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseUnary()
    {
        if (Match(TokenKind.Minus, TokenKind.Exclamation))
        {
            var op = Eat().Kind;
            var value = ParsePrimary();

            return new UnaryExpr(op, value);
        }

        return ParsePrimary();
    }

    private Expr ParsePrimary()
    {
        if (Match(
            TokenKind.NumberLiteral,
            TokenKind.StringLiteral,
            TokenKind.Nil,
            TokenKind.True,
            TokenKind.False))
        {
            return new LiteralExpr(Eat());
        }
        else if (AdvanceIf(TokenKind.OpenParenthesis))
        {
            var expr = ParseExpr();
            EatExpected(TokenKind.ClosedParenthesis);

            return expr;
        }
        else if (Match(TokenKind.Identifier))
        {
            return ParseIdentifier();
        }

        throw new NotImplementedException();
    }

    private Expr ParseIdentifier()
    {
        var identifier = Eat();
        if (AdvanceIf(TokenKind.OpenParenthesis))
        {
            var arguments = new List<Expr>();
            do
            {
                arguments.Add(ParseExpr());
            }
            while (AdvanceIf(TokenKind.Comma));

            EatExpected(TokenKind.ClosedParenthesis);

            return new CallExpr(identifier, arguments);
        }
        else if (_scope.ContainsVariable(identifier.Value))
        {
            return new VariableExpr(identifier);
        }
        else
        {
            var textArguments = new List<Expr>();
            var currentText = new StringBuilder();
            AdvanceIf(TokenKind.WhiteSpace);
            while (!ReachedEnd && !ReachedTextEnd())
            {
                if (AdvanceIf(TokenKind.WhiteSpace))
                {
                    var token = new Token(TokenKind.StringLiteral, currentText.ToString(), identifier.Position);
                    textArguments.Add(new LiteralExpr(token));
                    currentText.Clear();
                    continue;
                }

                currentText.Append(Eat().Value);
            }

            var finalToken = new Token(TokenKind.StringLiteral, currentText.ToString(), identifier.Position);
            textArguments.Add(new LiteralExpr(finalToken));

            return new CallExpr(identifier, textArguments);
        }
    }

    private bool ReachedTextEnd()
    {
        return MatchInclWhiteSpace(
            TokenKind.ClosedParenthesis,
            TokenKind.ClosedBrace,
            TokenKind.Pipe,
            TokenKind.And,
            TokenKind.Or,
            TokenKind.NewLine
        );
    }

    private bool AdvanceIf(TokenKind kind)
    {
        if (Match(kind))
        {
            Eat();
            return true;
        }

        return false;
    }

    private Token EatExpected(TokenKind kind)
    {
        if (Match(kind))
        {
            return Eat();
        }

        throw new ParseException($"Expected '{kind}' but got '{Current?.Kind}'.");
    }

    private Token Eat()
    {
        var toReturn = _tokens[_index];
        _index++;

        return toReturn;
    }

    private bool Match(params TokenKind[] kinds)
    {
        // Avoid skipping white space if white space is
        // the expected kind.
        if (!kinds.HasSingle(x => x == TokenKind.WhiteSpace))
            SkipWhiteSpace();

        return MatchInclWhiteSpace(kinds);
    }

    private bool MatchInclWhiteSpace(params TokenKind[] kinds)
    {
        return Current != null && kinds.Contains(Current.Kind);
    }

    private void SkipWhiteSpace()
    {
        while (Current?.Kind == TokenKind.WhiteSpace || Current?.Kind == TokenKind.NewLine)
            Eat();
    }
}