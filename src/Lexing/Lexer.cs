using System;
using System.Collections.Generic;
using System.Text;

namespace Shel.Lexing;

internal class Lexer
{
    private readonly string _source;
    private int _index;
    private (int line, int column) _pos = (1, 0);

    private char Current => _index < _source.Length
        ? _source[_index]
        : '\0';

    private char Peek => _index + 1 < _source.Length
        ? _source[_index + 1]
        : '\0';

    private char Previous => _index > 1
        ? _source[_index - 1]
        : '\0';

    private bool ReachedEnd => _index >= _source.Length;

    private Lexer(string input)
    {
        _source = input;
    }

    public static List<Token> Lex(string input)
    {
        var lexer = new Lexer(input);
        var tokens = new List<Token>();

        Token token;
        while ((token = lexer.Next()).Kind != TokenKind.EndOfFile)
            tokens.Add(token);

        return tokens;
    }

    private Token Next()
    {
        return Current switch
        {
            '+' => Build(TokenKind.Plus, Eat()),
            '-' => Build(TokenKind.Minus, Eat()),
            '*' => Build(TokenKind.Star, Eat()),
            '/' => Build(TokenKind.Slash, Eat()),
            '>' => Peek == '='
                ? Build(TokenKind.GreaterEquals, Eat(2))
                : Build(TokenKind.Greater, Eat()),
            '<' => Peek == '='
                ? Build(TokenKind.LessEquals, Eat(2))
                : Build(TokenKind.Less, Eat()),
            '=' => Peek == '='
                ? Build(TokenKind.EqualsEquals, Eat(2))
                : Build(TokenKind.Equals, Eat()),
            '!' => Peek == '='
                ? Build(TokenKind.NotEquals, Eat(2))
                : Build(TokenKind.Exclamation, Eat()),
            '&' => Peek == '&'
                ? Build(TokenKind.And, Eat(2))
                : Build(TokenKind.Unknown, Eat()),
            '|' => Peek == '|'
                ? Build(TokenKind.Or, Eat(2))
                : Build(TokenKind.Pipe, Eat()),
            '(' => Build(TokenKind.OpenParenthesis, Eat()),
            ')' => Build(TokenKind.ClosedParenthesis, Eat()),
            '[' => Build(TokenKind.OpenSquareBracket, Eat()),
            ']' => Build(TokenKind.ClosedSquareBracket, Eat()),
            '{' => Build(TokenKind.OpenBrace, Eat()),
            '}' => Build(TokenKind.ClosedBrace, Eat()),
            ':' => Build(TokenKind.Colon, Eat()),
            ',' => Build(TokenKind.Comma, Eat()),
            '.' => Build(TokenKind.Dot, Eat()),
            '~' => Build(TokenKind.Tilde, Eat()),
            '\0' => Build(TokenKind.EndOfFile, Eat()),
            _ => NextComplex(),
        };
    }

    private Token NextComplex()
    {
        if (char.IsWhiteSpace(Current))
        {
            return NextWhiteSpace();
        }
        else if (Current == '#')
        {
            var c = NextComment();
            return c;
        }
        else if (IsValidIdentifierStart(Current))
        {
            return NextIdentifier();
        }
        else if (char.IsDigit(Current))
        {
            return NextNumber();
        }
        else if (Current == '"' && Previous != '\\')
        {
            return NextString();
        }

        return Build(TokenKind.Unknown, Eat().ToString());
    }

    private Token NextWhiteSpace()
    {
        if (AdvanceIf('\n'))
        {
            return Build(TokenKind.NewLine, Environment.NewLine);
        }

        var value = new StringBuilder();
        while (char.IsWhiteSpace(Current))
        {
            value.Append(Eat());
        }

        return Build(
            TokenKind.WhiteSpace,
            value.ToString(),
            new(_pos.line, _pos.column - value.Length)
        );
    }

    private Token NextComment()
    {
        var value = new StringBuilder();
        while (!ReachedEnd && Current != '\n')
        {
            value.Append(Eat());
        }

        return Build(
            TokenKind.Comment,
            value.ToString(),
            new(_pos.line, _pos.column - value.Length)
        );
    }

    private Token NextIdentifier()
    {
        var value = new StringBuilder();
        value.Append(Eat());
        while (IsValidIdentifierMiddle(Current))
        {
            value.Append(Eat());
        }

        var kind = value.ToString() switch
        {
            "fn" => TokenKind.Fn,
            "let" => TokenKind.Let,
            "if" => TokenKind.If,
            "else" => TokenKind.Else,
            "for" => TokenKind.For,
            "return" => TokenKind.Return,
            "nil" => TokenKind.Nil,
            "true" => TokenKind.True,
            "false" => TokenKind.False,
            _ => TokenKind.Identifier,
        };

        return Build(
            kind,
            value.ToString(),
            new(_pos.line, _pos.column - value.Length)
        );
    }

    private Token NextNumber()
    {
        var value = new StringBuilder();
        bool isFloat = false;
        while (char.IsDigit(Current) || Current == '.')
        {
            if (Current == '.')
                isFloat = true;

            value.Append(Eat());
        }

        return Build(
            isFloat ? TokenKind.FloatLiteral : TokenKind.IntegerLiteral,
            value.ToString(),
            new(_pos.line, _pos.column - value.Length)
        );
    }

    private Token NextString()
    {
        Eat(); // Initial quote

        var value = new StringBuilder();
        while (!ReachedEnd && Current != '"')
        {
            if (AdvanceIf('\\'))
            {
                char c = Current switch
                {
                    'n' => '\n',
                    't' => '\t',
                    'r' => '\r',
                    '\\' => '\\',
                    _ => Current
                };
                Eat();

                value.Append(c);
            }

            value.Append(Eat());
        }

        Eat(); // Final quote

        return Build(
            TokenKind.StringLiteral,
            value.ToString(),
            new(_pos.line, _pos.column - value.Length)
        );
    }

    private static bool IsValidIdentifierStart(char c)
    {
        return char.IsLetter(c) || c == '_';
    }

    private static bool IsValidIdentifierMiddle(char c)
    {
        return char.IsLetter(c) || c == '_';
    }

    private Token Build(TokenKind kind, char value)
    {
        return Build(kind, value.ToString());
    }

    private Token Build(TokenKind kind, string value, TextPos? pos = null)
    {
        return new Token(
            kind,
            value,
            pos ?? new TextPos(_pos.line, _pos.column)
        );
    }

    private bool AdvanceIf(char c)
    {
        if (Current == c)
        {
            Eat();
            return true;
        }

        return false;
    }

    private string Eat(int count)
    {
        var result = "";
        for (int i = 0; i < count; i++)
        {
            result += Eat();
        }

        return result;
    }

    private char Eat()
    {
        char toReturn = Current;
        _index++;
        if (toReturn == '\n')
        {
            _pos.line++;
            _pos.column = 0;
        }
        else
        {
            _pos.column++;
        }

        return toReturn;
    }
}