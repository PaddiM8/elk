#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.ReadLine;
using Elk.Lexing;

#endregion

namespace Elk.Cli;

record ShellStyleInvocationInfo(
    string Name,
    string TextArguments,
    int StartIndex,
    int EndIndex)
{
    public int TextArgumentStartIndex
        => StartIndex + Name.Length + 1;
}

class HighlightHandler : IHighlightHandler
{
    public IEnumerable<ShellStyleInvocationInfo> LastShellStyleInvocations
        => _lastShellStyleInvocations;

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
    private int _length;
    private HighlightHandler? _innerHighlighter;
    private readonly List<ShellStyleInvocationInfo> _lastShellStyleInvocations = new();

    public HighlightHandler(ShellSession shell)
    {
        _shell = shell;
    }

    public string Highlight(string text)
    {
        _tokens = Lexer.Lex(text, "", out _, LexerMode.Preserve);
        _length = text.Length;
        _index = 0;
        _lastShellStyleInvocations.Clear();
        
        var builder = new StringBuilder();
        while (!ReachedEnd)
            builder.Append(Next());
        
        return builder.ToString();
    }

    private string Next()
    {
        if (Current?.Kind is TokenKind.Dot or TokenKind.DotDot or TokenKind.Tilde &&
            Peek?.Kind == TokenKind.Slash)
        {
            return NextPath();
        }

        if (Current?.Kind == TokenKind.Slash && Peek?.Kind == TokenKind.Identifier)
            return NextPath();

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
            if (value.ElementAtOrDefault(i - 1) != '\\' &&
                c == '$' &&
                value.ElementAtOrDefault(i + 1) == '{')
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

    private string NextIdentifier(List<string>? modulePath = null)
    {
        int startIndex = Current!.Position.Index;
        string identifier = Eat()!.Value.Trim();
        string plurality = identifier.EndsWith("!") ? "!" : "";
        identifier = identifier.TrimCharEnd('!');

        modulePath ??= new List<string>();
        modulePath.Add(identifier);

        if (_shell.ModuleExists(modulePath))
            return NextModule(modulePath);

        modulePath.RemoveAt(modulePath.Count - 1);

        if (_shell.StructExists(identifier))
            return Color(identifier + plurality, 96);

        if (_shell.VariableExists(identifier))
            return identifier + plurality;

        string textArguments = NextTextArguments();
        if (Current?.Kind != TokenKind.OpenParenthesis || textArguments.Length > 0)
        {
            _lastShellStyleInvocations.Add(
                new(
                    identifier,
                    textArguments,
                    startIndex, Current?.Position.Index ?? _length
                )
            );
        }

        var isCallable = _shell.FunctionExists(identifier, modulePath) || _shell.ProgramExists(identifier);
        int colorCode = isCallable ? 95 : 91;

        return textArguments.Length > 0
            ? Color(identifier, colorCode, null) + plurality + textArguments
            : Color(identifier, colorCode) + plurality;
    }

    private string NextModule(List<string> modulePath)
    {
        var builder = new StringBuilder();
        string name = Previous!.Value;
        if (!_shell.ModuleExists(modulePath))
        {
            builder.Append(Color(name, 91));

            return builder.ToString();
        }

        builder.Append(Color(name, 34));
        if (Current is not { Kind: TokenKind.ColonColon })
            return builder.ToString();

        builder.Append(Eat()!.Value);

        if (Current != null)
            builder.Append(NextIdentifier(modulePath));

        return builder.ToString();
    }

    private string NextPath()
    {
        int startIndex = Current!.Position.Index;
        var builder = new StringBuilder();
        while (!ReachedTextEnd() &&
               Current?.Kind is not (TokenKind.WhiteSpace or TokenKind.OpenParenthesis or TokenKind.OpenSquareBracket))
        {
            // If ".." is not before/after a slash, it is not a part of a path
            // and the loop should be stopped.
            if (Current?.Kind == TokenKind.DotDot &&
                Previous?.Kind != TokenKind.Slash &&
                Peek?.Kind != TokenKind.Slash)
            {
                break;
            }

            builder.Append(Eat()!.Value);
        }

        string identifier = builder.ToString();
        string textArguments = NextTextArguments();
        if (Current?.Kind != TokenKind.OpenParenthesis || textArguments.Length > 0)
        {
            _lastShellStyleInvocations.Add(
                new(
                    identifier,
                    textArguments,
                    startIndex, Current?.Position.Index ?? _length
                )
            );
        }

        int colorCode = _shell.ProgramExists(identifier) ? 95 : 91;

        return textArguments.Length > 0
            ? Color(identifier, colorCode, null) + textArguments
            : Color(identifier, colorCode);
    }

    private string NextTextArguments()
    {
        if (Current?.Kind != TokenKind.WhiteSpace)
            return "";

        var textArgumentBuilder = new StringBuilder();
        while (!ReachedTextEnd())
        {
            if (Current!.Kind is TokenKind.StringLiteral)
            {
                textArgumentBuilder.Append(NextStringLiteral(endColor: 36));
            }
            else if (Previous?.Kind != TokenKind.Backslash &&
                     Current!.Value == "$" &&
                     Peek?.Kind == TokenKind.OpenBrace)
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

        return Color(textArgumentBuilder.ToString(), 36);
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
        var reachedComment = Previous?.Kind == TokenKind.WhiteSpace && Current?.Kind == TokenKind.Comment;

        return ReachedEnd || reachedComment || Current?.Kind is
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
