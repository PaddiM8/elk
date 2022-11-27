#region

using System.Collections.Generic;
using System.Linq;
using System.Text;
using BetterReadLine;
using Elk.Lexing;
using Elk.Std.Bindings;

#endregion

namespace Elk.Cli;

class HighlightHandler : IHighlightHandler
{
    private Token? Peek
        => _tokens.ElementAtOrDefault(_index + 1);
    
    private Token? Current
        => _tokens.ElementAtOrDefault(_index);
    
    private Token? Previous
        => _tokens.ElementAtOrDefault(_index - 1);

    private bool ReachedEnd
        => _index >= _tokens.Count;
    
    private readonly ShellSession _shell;
    private List<Token> _tokens = null!;
    private int _index;
    private HighlightHandler? _innerHighlighter;

    public HighlightHandler(ShellSession shell)
    {
        _shell = shell;
    }

    public string Highlight(string text)
    {
        _tokens = Lexer.Lex(text, "", out _, LexerMode.Preserve);
        _index = 0;
        
        var builder = new StringBuilder();
        while (!ReachedEnd)
            builder.Append(Next());
        
        return builder.ToString();
    }

    private string Next()
    {
        return Current?.Kind switch
        {
            >= TokenKind.Not and <= TokenKind.New => NextKeyword(),
            TokenKind.IntegerLiteral or TokenKind.FloatLiteral => NextNumberLiteral(),
            TokenKind.Comment => NextComment(),
            TokenKind.StringLiteral => NextStringLiteral(),
            TokenKind.Identifier => NextIdentifier(),
            TokenKind.Arrow => NextFieldAccess(),
            _ => Eat()!.Value,
        };
    }

    private string NextKeyword()
    {
        var keyword = Eat()!;
        var builder = new StringBuilder();
        builder.Append(Color(keyword.Value, 31));
        
        if (keyword.Kind == TokenKind.Fn)
        {
            while (!ReachedEnd && Current?.Kind is not TokenKind.OpenBrace and not TokenKind.Colon)
                builder.Append(Eat()!.Value);
        }
        else if (keyword.Kind is TokenKind.Let or TokenKind.Alias)
        {
            while (!ReachedEnd && Current?.Kind is not TokenKind.Equals)
                builder.Append(Eat()!.Value);
        }
        else if (keyword.Kind == TokenKind.For)
        {
            while (!ReachedEnd && Current?.Kind is not TokenKind.In)
                builder.Append(Eat()!.Value);
        }
        else if (keyword.Kind == TokenKind.With)
        {
            while (!ReachedEnd && Current?.Kind != TokenKind.NewLine && Current?.Value != "from")
                builder.Append(Eat()!.Value);

            if (Current?.Value == "from")
            {
                builder.Append(Color(Eat()!.Value, 31));
                if (Current?.Kind == TokenKind.WhiteSpace)
                    builder.Append(Eat()!.Value);
                if (Current?.Kind == TokenKind.Identifier)
                    builder.Append(Eat()!.Value);
            }
        }
        else if (keyword.Kind is TokenKind.Using or TokenKind.Unalias or TokenKind.Module)
        {
            if (Current?.Kind == TokenKind.WhiteSpace)
                builder.Append(Eat()!.Value);
            if (Current?.Kind == TokenKind.Identifier)
                builder.Append(Eat()!.Value);
        }
        else if (keyword.Kind == TokenKind.Struct)
        {
            while (!ReachedEnd && Current?.Kind != TokenKind.ClosedParenthesis)
                builder.Append(Eat()!.Value);
        }

        return builder.ToString();
    }

    private string NextNumberLiteral()
        => Color(Eat()!.Value, 33);

    private string NextComment()
        => Color(Eat()!.Value, 90);

    private string NextStringLiteral(int endColor = 0)
    {
        string value = Eat()!.Value;
        var builder = new StringBuilder();
        for (int i = 0; i < value.Length; i++)
        {
            char? c = value[i];
            if (c == '$' && value.ElementAtOrDefault(i + 1) == '{')
            {
                i += 2;
                c = value.ElementAtOrDefault(i);
                builder.Append(Color("${", 37));

                int interpolationContentStart = i;
                int openBraces = 1;
                bool inString = false;
                while (i < value.Length && openBraces != 0)
                {
                    if (c == '"' && value[i - 1] != '\\')
                        inString = !inString;
                    else if (!inString && c == '{')
                        openBraces++;
                    else if (!inString && c == '}')
                        openBraces--;
                    
                    i++;
                    c = value.ElementAtOrDefault(i);
                }

                _innerHighlighter ??= new HighlightHandler(_shell);
                
                if (value[i - 1] == '}')
                {
                    int interpolationContentEnd = i - 1;
                    builder.Append(
                        _innerHighlighter.Highlight(value[interpolationContentStart..interpolationContentEnd])
                    );
                    builder.Append(Color("}", 37, endColor: 93));
                }
                else if (i <= value.Length && i != interpolationContentStart)
                {
                    builder.Append(_innerHighlighter.Highlight(value[interpolationContentStart..i]));
                }
            }

            if (c.HasValue)
                builder.Append(c);
        }

        return Color(builder.ToString(), 93, endColor);
    }

    private string NextIdentifier()
    {
        string identifier = Eat()!.Value;
        
        if (_shell.StructExists(identifier.Trim()) || StdBindings.HasRuntimeType(identifier.Trim()))
            return Color(identifier, 96);

        if (_shell.VariableExists(identifier.Trim()))
            return identifier;

        var textArgumentBuilder = new StringBuilder();
        if (Current?.Kind == TokenKind.WhiteSpace)
        {
            while (!ReachedTextEnd())
            {
                if (Current!.Kind == TokenKind.StringLiteral)
                {
                    textArgumentBuilder.Append(NextStringLiteral(endColor: 36));
                }
                else if (Current!.Value == "$" && Peek?.Kind == TokenKind.OpenBrace)
                {
                    textArgumentBuilder.Append(NextInterpolation(endColor: 36));
                }
                else if (Current?.Kind == TokenKind.Backslash)
                {
                    textArgumentBuilder.Append(Color(Eat()!.Value, 0, endColor: 36));
                }
                else
                {
                    textArgumentBuilder.Append(Eat()!.Value);
                }
            }
        }

        if (textArgumentBuilder.Length > 0)
        {
            return Color(identifier, 95, null) + Color(textArgumentBuilder.ToString(), 36);
        }

        return Color(identifier, 95);
    }

    private string NextInterpolation(int endColor = 0)
    {
        var builder = new StringBuilder();
        Eat(); // $
        Eat(); // {
        builder.Append(Color("${", 37));

        int openBraces = 1;
        while (!ReachedEnd && openBraces > 0)
        {
            if (Current?.Kind == TokenKind.OpenBrace)
                openBraces++;
            else if (Current?.Kind == TokenKind.ClosedBrace)
                openBraces--;

            if (openBraces == 0)
            {
                Eat(); // }
                builder.Append(Color("}", 37, endColor));
                break;
            }

            builder.Append(Next());
        }

        return builder.ToString();
    }

    private string NextFieldAccess()
    {
        var builder = new StringBuilder();
        builder.Append(Eat()!.Value); // ->
        if (Current?.Kind == TokenKind.Identifier)
            builder.Append(Eat()!.Value);

        return builder.ToString();
    }

    private bool ReachedTextEnd()
    {
        return ReachedEnd || Current?.Kind is 
                TokenKind.AmpersandAmpersand or
                TokenKind.PipePipe or
                TokenKind.EqualsGreater or
                TokenKind.ClosedParenthesis or
                TokenKind.OpenBrace or
                TokenKind.ClosedBrace or
                TokenKind.Pipe or
                TokenKind.Semicolon or
                TokenKind.NewLine
            && Previous?.Kind != TokenKind.Backslash;
    }

    private static string Color(string text, int color, int? endColor = 0)
        => endColor == null
            ? $"\x1b[{color}m{text}"
            : $"\x1b[{color}m{text}\x1b[{endColor}m";

    private Token? Eat()
    {
        var token = Current;
        _index++;

        return token;
    }
}