using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Shel.Interpreting;
using Shel.Lexing;

namespace Shel.Parsing;

internal class Parser
{
    private readonly List<Token> _tokens;
    private int _index;
    private Scope _scope;
    private bool _allowEndOfExpression = false;

    private Token? Current => _index < _tokens.Count
        ? _tokens[_index]
        : null;

    private Token? Previous => _index - 1 > 0
        ? _tokens[_index - 1]
        : null;

    private bool ReachedEnd => _index >= _tokens.Count;

    private Parser(List<Token> tokens, GlobalScope scope)
    {
        _tokens = tokens;
        _scope = scope;
    }

    public static List<Expr> Parse(List<Token> tokens, GlobalScope scope)
    {
        var parser = new Parser(tokens, scope);
        var expressions = new List<Expr>();
        while (!parser.ReachedEnd)
        {
            var expr = parser.ParseExpr();
            expr.IsRoot = true;
            expressions.Add(expr);
        }

        return expressions;
    }

    private Expr ParseExpr()
    {
        if (Match(TokenKind.Fn))
        {
            return ParseFn();
        }
        else if (Match(TokenKind.Return))
        {
            return ParseReturn();
        }

        return ParsePipe();
    }

    private Expr ParseFn()
    {
        EatExpected(TokenKind.Fn);
        var identifier = EatExpected(TokenKind.Identifier);

        var parameters = ParseParameterList();
        var functionScope = new LocalScope(_scope);
        foreach (var parameter in parameters)
        {
            functionScope.AddVariable(parameter.Value, RuntimeNil.Value);
        }

        var block = ParseBlockOrSingle(functionScope);
        var function = new FunctionExpr(identifier, parameters, block);

        _scope.GlobalScope.AddFunction(function);

        return function;
    }

    private Expr ParseReturn()
    {
        EatExpected(TokenKind.Return);

        return new ReturnExpr(ParseExpr());
    }

    private List<Token> ParseParameterList()
    {
        EatExpected(TokenKind.OpenParenthesis);
        var parameters = new List<Token>();

        do
        {
            parameters.Add(EatExpected(TokenKind.Identifier));
        }
        while(AdvanceIf(TokenKind.Comma) && !Match(TokenKind.ClosedParenthesis));

        EatExpected(TokenKind.ClosedParenthesis);

        return parameters;
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
        var left = ParseRange();

        while (Match(
            TokenKind.Greater, 
            TokenKind.GreaterEquals,
            TokenKind.Less, 
            TokenKind.LessEquals,
            TokenKind.EqualsEquals,
            TokenKind.NotEquals))
        {
            var op = Eat().Kind;
            var right = ParseRange();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseRange()
    {
        if (Peek()?.Kind is not TokenKind.Slash && AdvanceIf(TokenKind.DotDot))
        {
            return new RangeExpr(null, ParseAdditive());
        }

        var left = ParseAdditive();
        if (Peek()?.Kind is not TokenKind.Slash && AdvanceIf(TokenKind.DotDot))
        {
            bool allowedEnd = _allowEndOfExpression;
            _allowEndOfExpression = true;
            var right = ParseAdditive();
            _allowEndOfExpression = allowedEnd;

            if (right is EmptyExpr)
            {
                return new RangeExpr(left, null);
            }

            return new RangeExpr(left, right);
        }

        return left;
    }

    private Expr ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (Match(TokenKind.Plus, TokenKind.Minus))
        {
            var op = Eat().Kind;
            var right = ParseMultiplicative();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseMultiplicative()
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
            var value = ParseIndexer();

            return new UnaryExpr(op, value);
        }

        return ParseIndexer();
    }

    private Expr ParseIndexer()
    {
        var expr = ParsePrimary();
        if (AdvanceIf(TokenKind.OpenSquareBracket))
        {
            var index = ParseExpr();
            EatExpected(TokenKind.ClosedSquareBracket);

            return new IndexerExpr(expr, index);
        }

        return expr;
    }

    private Expr ParsePrimary()
    {
        if (Match(
            TokenKind.IntegerLiteral,
            TokenKind.FloatLiteral,
            TokenKind.StringLiteral,
            TokenKind.Nil,
            TokenKind.True,
            TokenKind.False))
        {
            return Current!.Kind switch
            {
                TokenKind.IntegerLiteral => new IntegerLiteralExpr(Eat()),
                TokenKind.FloatLiteral => new FloatLiteralExpr(Eat()),
                _ => new LiteralExpr(Eat()),
            };
        }
        else if (AdvanceIf(TokenKind.OpenParenthesis))
        {
            var expr = ParseExpr();
            EatExpected(TokenKind.ClosedParenthesis);

            return expr;
        }
        else if (Match(TokenKind.Let))
        {
            return ParseLet();
        }
        else if (Match(TokenKind.If))
        {
            return ParseIf();
        }
        else if (Match(TokenKind.OpenSquareBracket))
        {
            return ParseList();
        }
        else if (Match(TokenKind.OpenBrace))
        {
            return ParseBlock();
        }
        else if (Match(TokenKind.Identifier, TokenKind.Dot, TokenKind.DotDot, TokenKind.Slash, TokenKind.Tilde))
        {
            return ParseIdentifier();
        }

        if (_allowEndOfExpression)
        {
            return new EmptyExpr();
        }
        else
        {
            throw Current == null
                ? Error($"Unexpected end of expression")
                : Error($"Unexpected token: '{Current?.Kind}'");
        }
    }

    private Expr ParseLet()
    {
        EatExpected(TokenKind.Let);

        var identifier = EatExpected(TokenKind.Identifier);
        EatExpected(TokenKind.Equals);
        _scope.AddVariable(identifier.Value, RuntimeNil.Value);

        return new LetExpr(identifier, ParseExpr());
    }

    private Expr ParseIf()
    {
        EatExpected(TokenKind.If);

        var condition = ParseExpr();
        var thenBranch = ParseBlockOrSingle();
        var elseBranch = AdvanceIf(TokenKind.Else)
            ? ParseExpr()
            : null;
        
        return new IfExpr(condition, thenBranch, elseBranch);
    }

    private Expr ParseList()
    {
        var pos = EatExpected(TokenKind.OpenSquareBracket).Position;

        var expressions = new List<Expr>();
        do
        {
            if (Match(TokenKind.ClosedSquareBracket))
                break;

            expressions.Add(ParseExpr());
        }
        while (AdvanceIf(TokenKind.Comma));

        EatExpected(TokenKind.ClosedSquareBracket);

        return new ListExpr(expressions, pos);
    }

    private BlockExpr ParseBlockOrSingle(LocalScope? scope = null)
    {
        if (AdvanceIf(TokenKind.Colon))
        {
            _scope = scope ?? new LocalScope(_scope);
            var expr = ParseExpr();
            _scope = _scope.Parent!;

            return new BlockExpr(
                new() { expr },
                expr.Position
            );
        }

        return ParseBlock(scope);
    }

    private BlockExpr ParseBlock(LocalScope? scope = null)
    {
        EatExpected(TokenKind.OpenBrace);

        var pos = Current!.Position;
        _scope = scope ?? new LocalScope(_scope);

        var expressions = new List<Expr>();
        while (!AdvanceIf(TokenKind.ClosedBrace))
            expressions.Add(ParseExpr());

        _scope = _scope.Parent!;

        return new BlockExpr(expressions, pos);
    }

    private Expr ParseIdentifier()
    {
        var pos = Current?.Position ?? new TextPos(0, 0);
        var identifier = new Token(TokenKind.Identifier, ParsePath(), pos);
        if (AdvanceIf(TokenKind.OpenParenthesis))
        {
            var arguments = new List<Expr>();
            do
            {
                if (!Match(TokenKind.ClosedParenthesis))
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

                var next = Peek();
                if (Match(TokenKind.Tilde) &&
                    (next == null || next.Kind == TokenKind.Slash || next.Kind == TokenKind.WhiteSpace))
                {
                    Eat();
                    currentText.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                }
                else
                {
                    currentText.Append(Eat().Value);
                }
            }

            // There will still be some text left that needs to be added since
            // currentText is only moved to textArguments when encountering a space,
            // which normally are not present at the end.
            if (currentText.Length > 0)
            {
                var finalToken = new Token(TokenKind.StringLiteral, currentText.ToString(), identifier.Position);
                textArguments.Add(new LiteralExpr(finalToken));
            }

            return new CallExpr(identifier, textArguments);
        }
    }

    private string ParsePath()
    {
        var value = new StringBuilder();
        while (!ReachedTextEnd() &&
               !MatchInclWhiteSpace(TokenKind.WhiteSpace, TokenKind.OpenParenthesis, TokenKind.OpenSquareBracket))
        {
            // If ".." is not before/after a slash, it is not a part of a path
            // and the loop should be stopped.
            if (Match(TokenKind.DotDot) &&
                Previous?.Kind is not TokenKind.Slash &&
                Peek()?.Kind is not TokenKind.Slash)
            {
                break;
            }

            var token = Eat();
            value.Append(
                token.Kind == TokenKind.Tilde
                    ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                    : token.Value
            );
        }

        return value.ToString();
    }

    private bool ReachedTextEnd()
    {
        return ReachedEnd || MatchInclWhiteSpace(
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

        throw Error($"Expected '{kind}' but got '{Current?.Kind}'");
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

    private Token? Peek(int length = 1)
        => _tokens.Count > _index + length
            ? _tokens[_index + length]
            : null;

    private bool MatchInclWhiteSpace(params TokenKind[] kinds)
        => Current != null && kinds.Contains(Current.Kind);

    private ParseException Error(string message)
        => Current == null && _index > 0
            ? new(_tokens[_index - 1].Position, message)
            : new(Current?.Position ?? new TextPos(0, 0), message);

    private void SkipWhiteSpace()
    {
        while (Current?.Kind == TokenKind.WhiteSpace ||
            Current?.Kind == TokenKind.NewLine ||
            Current?.Kind == TokenKind.Comment)
        {
            Eat();
        }
    }
}