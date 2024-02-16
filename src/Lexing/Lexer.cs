#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

#endregion

namespace Elk.Lexing;

public record LexError(string Message, TextPos StartPosition, TextPos EndPosition);

public enum LexerMode
{
    Default,
    Preserve,
}

public class Lexer
{
    private char Current => _index < _source.Length
        ? _source[_index]
        : '\0';

    private char Peek => _index + 1 < _source.Length
        ? _source[_index + 1]
        : '\0';

    private char Previous => _index >= 1
        ? _source[_index - 1]
        : '\0';

    private bool ReachedEnd => _index >= _source.Length;

    private readonly string _source;
    private int _index;
    private (int line, int column) _pos;
    private readonly string? _filePath;
    private LexError? _error;
    private readonly LexerMode _mode;

    private Lexer(string input, TextPos startPos, LexerMode mode)
    {
        _source = input;
        _pos = (startPos.Line, startPos.Column);
        _filePath = startPos.FilePath;
        _mode = mode;
    }

    public static List<Token> Lex(
        string input,
        TextPos startPos,
        out LexError? error,
        LexerMode mode = LexerMode.Default)
    {
        var lexer = new Lexer(input, startPos, mode);
        var tokens = new List<Token>();

        Token token;
        while ((token = lexer.Next()).Kind != TokenKind.EndOfFile)
            tokens.Add(token);

        error = lexer._error;

        return tokens;
    }

    public static List<Token> Lex(
        string input,
        string? filePath,
        out LexError? error,
        LexerMode mode = LexerMode.Default)
    {
        var result = Lex(input, new TextPos(1, 1, 0, filePath), out var innerError, mode);
        error = innerError;

        return result;
    }

    private Token Next()
    {
        return Current switch
        {
            '+' => Peek == '='
                ? Build(TokenKind.PlusEquals, Eat(2))
                : Build(TokenKind.Plus, Eat()),
            '-' => Peek switch
            {
                '=' => Build(TokenKind.MinusEquals, Eat(2)),
                '>' => Build(TokenKind.Arrow, Eat(2)),
                _ => Build(TokenKind.Minus, Eat()),
            },
            '*' => Peek == '='
                ? Build(TokenKind.StarEquals, Eat(2))
                : Build(TokenKind.Star, Eat()),
            '/' => Peek == '='
                ? Build(TokenKind.SlashEquals, Eat(2))
                : Build(TokenKind.Slash, Eat()),
            '%' => Build(TokenKind.Percent, Eat()),
            '^' => Build(TokenKind.Caret, Eat()),
            '>' => Peek == '='
                ? Build(TokenKind.GreaterEquals, Eat(2))
                : Build(TokenKind.Greater, Eat()),
            '<' => Peek == '='
                ? Build(TokenKind.LessEquals, Eat(2))
                : Build(TokenKind.Less, Eat()),
            '=' => Peek switch
            {
                '=' => Build(TokenKind.EqualsEquals, Eat(2)),
                '>' => Build(TokenKind.EqualsGreater, Eat(2)),
                _ => Build(TokenKind.Equals, Eat()),
            },
            '!' => Peek == '='
                ? Build(TokenKind.NotEquals, Eat(2))
                : Build(TokenKind.Unknown, Eat()),
            '&' => Peek == '&'
                ? Build(TokenKind.AmpersandAmpersand, Eat(2))
                : Build(TokenKind.Ampersand, Eat()),
            '|' => Peek == '|'
                ? Build(TokenKind.PipePipe, Eat(2))
                : NextPipe(),
            '?' => NextCoalescing(),
            '(' => Build(TokenKind.OpenParenthesis, Eat()),
            ')' => Build(TokenKind.ClosedParenthesis, Eat()),
            '[' => Build(TokenKind.OpenSquareBracket, Eat()),
            ']' => Build(TokenKind.ClosedSquareBracket, Eat()),
            '{' => Build(TokenKind.OpenBrace, Eat()),
            '}' => Build(TokenKind.ClosedBrace, Eat()),
            ':' => Peek == ':'
                ? Build(TokenKind.ColonColon, Eat(2))
                : Build(TokenKind.Colon, Eat()),
            ',' => Build(TokenKind.Comma, Eat()),
            '.' => Peek == '.'
                ? Build(TokenKind.DotDot, Eat(2))
                : Build(TokenKind.Dot, Eat()),
            '~' => Build(TokenKind.Tilde, Eat()),
            ';' => Build(TokenKind.Semicolon, Eat()),
            '\0' => Build(TokenKind.EndOfFile, Eat()),
            _ => NextComplex(),
        };
    }

    private Token NextCoalescing()
    {
        var first = Eat();
        if (Current != '?')
            return Build(TokenKind.Unknown, first);

        return Peek == '='
            ? Build(TokenKind.QuestionQuestionEquals, first + Eat(2))
            : Build(TokenKind.QuestionQuestion, first + Eat(1));
    }

    private Token NextPipe()
    {
        var remainingLength = _source.Length - _index - 1;
        if (remainingLength < 4)
            return Build(TokenKind.Pipe, Eat());

        // Get the next 4 or 5 characters. If there is
        // a 5th one, we want to grab that as well to
        // make sure it's just white space.
        var substring = _source.Substring(
            _index,
            Math.Min(remainingLength, 5)
        );
        var trimmed = substring.Trim();

        if (trimmed == "|err")
            return Build(TokenKind.PipeErr, Eat(4));

        if (trimmed == "|all")
            return Build(TokenKind.PipeAll, Eat(4));

        return Build(TokenKind.Pipe, Eat());
    }

    private Token NextComplex()
    {
        if (Current == '$' && Peek == ':')
            return NextBashLiteral();

        if (Current == '\\')
        {
            if (Peek == '\n' && _mode != LexerMode.Preserve)
            {
                Eat(2);

                return Build(TokenKind.WhiteSpace, " ");
            }

            return Build(TokenKind.Backslash, Eat());
        }

        if (char.IsWhiteSpace(Current))
        {
            return NextWhiteSpace();
        }

        if (Current == '#')
        {
            var c = NextComment();
            return c;
        }

        if (Current == '"' && Previous != '\\')
        {
            return NextString();
        }

        if (Current == '\'' && Previous != '\\')
        {
            return NextSingleQuoteString();
        }

        if (IsValidIdentifierStart(Current))
        {
            return NextIdentifier();
        }

        return Build(TokenKind.Unknown, Eat().ToString());
    }

    private Token NextBashLiteral()
    {
        var builder = new StringBuilder();
        while (Current is not ('\n' or '\0'))
            builder.Append(Eat());

        return Build(TokenKind.BashLiteral, builder.ToString());
    }

    private Token NextWhiteSpace()
    {
        if (AdvanceIf('\n'))
            return Build(TokenKind.NewLine, Environment.NewLine);

        var startIndex = _index;
        var startColumn = _pos.column;
        var value = new StringBuilder();
        while (char.IsWhiteSpace(Current))
            value.Append(Eat());

        return Build(
            TokenKind.WhiteSpace,
            value.ToString(),
            new(_pos.line, startColumn, startIndex, _filePath)
        );
    }

    private Token NextComment()
    {
        var startLine = _pos.line;
        var startIndex = _index;
        var startColumn = _pos.column;
        var value = new StringBuilder();
        while (!ReachedEnd && Current != '\n')
            value.Append(Eat());

        return Build(
            TokenKind.Comment,
            value.ToString(),
            new(startLine, startColumn, startIndex, _filePath)
        );
    }

    private Token NextIdentifier()
    {
        var startIndex = _index;
        var startColumn = _pos.column;
        var value = new StringBuilder();
        value.Append(Eat());

        // Question marks are generally not allowed in identifiers,
        // but we still need to allow '$?', since that's used for
        // exit codes.
        if (Previous == '$' && Current == '?')
        {
            value.Append(Eat());

            return Build(
                TokenKind.Identifier,
                value.ToString(),
                new(_pos.line, startColumn, startIndex, _filePath)
            );
        }

        while (IsValidIdentifierMiddle(Current, Peek))
        {
            if (Current == '.' && Peek == '.')
                break;

            value.Append(Eat());
        }

        var kind = value.ToString() switch
        {
            "not" => TokenKind.Not,
            "and" => TokenKind.And,
            "or" => TokenKind.Or,
            "fn" => TokenKind.Fn,
            "let" => TokenKind.Let,
            "if" => TokenKind.If,
            "else" => TokenKind.Else,
            "for" => TokenKind.For,
            "while" => TokenKind.While,
            "in" => TokenKind.In,
            "return" => TokenKind.Return,
            "break" => TokenKind.Break,
            "continue" => TokenKind.Continue,
            "with" => TokenKind.With,
            "using" => TokenKind.Using,
            "nil" => TokenKind.Nil,
            "true" => TokenKind.True,
            "false" => TokenKind.False,
            "alias" => TokenKind.Alias,
            "unalias" => TokenKind.Unalias,
            "module" => TokenKind.Module,
            "struct" => TokenKind.Struct,
            "new" => TokenKind.New,
            "try" => TokenKind.Try,
            "catch" => TokenKind.Catch,
            "throw" => TokenKind.Throw,
            "pub" => TokenKind.Pub,
            _ => TokenKind.Identifier,
        };

        var valueString = value.ToString();
        var foundDot = false;
        bool IsValidNumberChar(char c, int i)
        {
            var isBaseSpecifier = i == 1 && valueString.StartsWith('0') && c is 'b' or 'o' or 'x';
            var isDecimalComma = i != 0 && c == '.' && !foundDot;
            if (isDecimalComma) foundDot = true;

            return isBaseSpecifier || isDecimalComma || char.IsDigit(c);
        }

        var isNumber = valueString
            .WithIndex()
            .All(x => IsValidNumberChar(x.item, x.index));
        if (isNumber)
            kind = foundDot ? TokenKind.FloatLiteral : TokenKind.IntegerLiteral;

        return Build(
            kind,
            valueString,
            new(_pos.line, startColumn, startIndex, _filePath)
        );
    }

    private Token NextString()
    {
        var startIndex = _index;
        var startColumn = _pos.column;
        var value = new StringBuilder();
        Eat(); // Initial quote

        if (_mode == LexerMode.Preserve)
            value.Append('"');

        while (!ReachedEnd && Current != '"')
        {
            if (_mode == LexerMode.Preserve && Current == '\\' && Peek == '"')
            {
                value.Append(Eat(2));
                continue;
            }

            if (_mode != LexerMode.Preserve && AdvanceIf('\\'))
            {
                var c = Current switch
                {
                    'b' => '\b',
                    'f' => '\f',
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    'v' => '\v',
                    '0' => '\0',
                    _ => Current,
                };
                Eat();

                // Keep the backslash for dollar signs to allow
                // the string interpolation parser to know when
                // a brace is escaped.
                if (c == '$')
                    value.Append('\\');

                if (c == 'x')
                {
                    var hex = new StringBuilder();
                    for (var i = 0; i < 4; i++)
                    {
                        if (!Current.IsHex())
                            break;

                        hex.Append(Eat());
                    }

                    var charValue = int.Parse(hex.ToString(), NumberStyles.HexNumber);
                    value.Append(Convert.ToChar(charValue));
                    continue;
                }

                value.Append(c);
            }
            else if (Current == '$' && Peek == '{')
            {
                value.Append(Eat());
                value.Append(Eat());
                var openBraces = 1;
                char? inStringSymbol = null;

                // This is necessary to handle string literals inside interpolation environments
                while (!ReachedEnd && openBraces > 0)
                {
                    var isClosingQuote =
                        (inStringSymbol == '"' && Current == '"') ||
                        (inStringSymbol == '\'' && Current == '\'');

                    if (inStringSymbol == null && Current is '"' or '\'')
                    {
                        inStringSymbol = Current;
                    }
                    else if (isClosingQuote && Previous != '\\')
                    {
                        inStringSymbol = null;
                    }

                    if (!inStringSymbol.HasValue && Current == '{')
                        openBraces++;
                    if (!inStringSymbol.HasValue && Current == '}')
                        openBraces--;

                    value.Append(Eat());
                }

            }
            else
            {
                value.Append(Eat());
            }
        }

        if (Current != '"')
        {
            Error("Unterminated string literal");
        }
        else
        {
            Eat(); // Final quote
            if (_mode == LexerMode.Preserve)
                value.Append('"');
        }

        return Build(
            TokenKind.DoubleQuoteStringLiteral,
            value.ToString(),
            new(_pos.line, startColumn, startIndex, _filePath)
        );
    }

    private Token NextSingleQuoteString()
    {
        var startIndex = _index;
        var startColumn = _pos.column;
        var value = new StringBuilder();
        Eat(); // Quote

        if (_mode == LexerMode.Preserve)
            value.Append('\'');

        while (!ReachedEnd && Current != '\'')
        {
            if (Current == '\\' && Peek == '\'')
            {
                if (_mode == LexerMode.Preserve)
                    value.Append('\\');

                value.Append('\'');
                Eat();
                Eat();
                continue;
            }

            value.Append(Eat());
        }

        if (Current != '\'')
        {
            Error("Unterminated string literal");
        }
        else
        {
            Eat(); // Final quote
            if (_mode == LexerMode.Preserve)
                value.Append('\'');
        }

        return Build(
            TokenKind.SingleQuoteStringLiteral,
            value.ToString(),
            new(_pos.line, startColumn, startIndex, _filePath)
        );
    }

    private static bool IsValidIdentifierStart(char c)
        => !"+-*/%^><=&|?()[]{}:;~\\\n\t\v\0\r\",. ".Contains(c);

    private static bool IsValidIdentifierMiddle(char c, char next)
        => c != '$' && (IsValidIdentifierStart(c) ||
            c == '-' && next != '>' ||
            "+*%^.".Contains(c) ||
            char.IsDigit(c));

    private Token Build(TokenKind kind, char value)
    {
        return Build(kind, value.ToString());
    }

    private Token Build(TokenKind kind, string value, TextPos? pos = null)
    {
        return new Token(
            kind,
            value,
            pos ?? new TextPos(
                _pos.line,
                _pos.column - value.Length,
                _index - value.Length,
                _filePath
            )
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
        for (var i = 0; i < count; i++)
            result += Eat();

        return result;
    }

    private char Eat()
    {
        var toReturn = Current;
        _index++;
        if (toReturn == '\n')
        {
            _pos.line++;
            _pos.column = 1;
        }
        else
        {
            _pos.column++;
        }

        return toReturn;
    }

    private void Error(string message)
    {
        var pos = new TextPos(_pos.line, _pos.column, _index, _filePath);
        _error = new LexError(
            message,
            pos,
            pos with
            {
                Column = pos.Column + 1,
                Index = pos.Index + 1,
            }
        );
    }
}