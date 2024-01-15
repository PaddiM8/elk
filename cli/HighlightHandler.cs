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
    TextArgumentsInfo TextArgumentsInfo,
    int StartIndex,
    int EndIndex)
{
    public int TextArgumentStartIndex
        => StartIndex + Name.Length + 1;
}

record TextArgumentsInfo(
    IList<string> Arguments,
    int ActiveArgumentIndex
);

class HighlightHandler(ShellSession shell) : IHighlightHandler
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

    private List<Token> _tokens = null!;
    private int _index;
    private int _length;
    private int _caret;
    private HighlightHandler? _innerHighlighter;
    private readonly List<ShellStyleInvocationInfo> _lastShellStyleInvocations = [];
    private readonly HashSet<string> _unevaluatedVariables = [];

    public string Highlight(string text, int caret)
        => Highlight(text, caret, null);

    private string Highlight(string text, int caret, HashSet<string>? unevaluatedVariables)
    {
        _tokens = Lexer.Lex(text, "", out _, LexerMode.Preserve);
        _index = 0;
        _length = text.Length;
        _caret = caret;
        _lastShellStyleInvocations.Clear();
        _unevaluatedVariables.Clear();
        if (unevaluatedVariables != null)
        {
            foreach (var variable in unevaluatedVariables)
                _unevaluatedVariables.Add(variable);
        }

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
            >= TokenKind.Not and <= TokenKind.Pub => NextKeyword(),
            TokenKind.IntegerLiteral or TokenKind.FloatLiteral => NextNumberLiteral(),
            TokenKind.Comment => NextComment(),
            TokenKind.DoubleQuoteStringLiteral or TokenKind.SingleQuoteStringLiteral => NextStringLiteral(),
            TokenKind.Identifier => NextIdentifier(),
            TokenKind.Arrow => NextFieldAccess(),
            TokenKind.EqualsGreater => NextClosure(),
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
            {
                var token = Eat()!;
                if (token.Kind == TokenKind.Identifier)
                    _unevaluatedVariables.Add(token.Value);

                builder.Append(token.Value);
            }
        }
        else if (keyword.Kind is TokenKind.Let or TokenKind.Alias)
        {
            while (!ReachedEnd && Current?.Kind is not TokenKind.Equals)
            {
                var token = Eat()!;
                if (token.Kind == TokenKind.Identifier)
                    _unevaluatedVariables.Add(token.Value);

                builder.Append(token.Value);
            }
        }
        else if (keyword.Kind == TokenKind.For)
        {
            while (!ReachedEnd && Current?.Kind is not TokenKind.In)
            {
                var token = Eat()!;
                if (token.Kind == TokenKind.Identifier)
                    _unevaluatedVariables.Add(token.Value);

                builder.Append(token.Value);
            }
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
            if (Current?.Kind == TokenKind.WhiteSpace)
                builder.Append(Eat()!.Value);
            if (Current?.Kind == TokenKind.Identifier)
                _unevaluatedVariables.Add(Current.Value);

            while (!ReachedEnd && Current?.Kind != TokenKind.ClosedParenthesis)
                builder.Append(Eat()!.Value);
        }
        else if (keyword.Kind == TokenKind.Catch)
        {
            if (Current?.Kind == TokenKind.WhiteSpace)
                builder.Append(Eat()!.Value);
            if (Current?.Kind == TokenKind.Identifier)
                _unevaluatedVariables.Add(Current.Value);
        }

        return builder.ToString();
    }

    private string NextNumberLiteral()
        => Color(Eat()!.Value, 33);

    private string NextComment()
        => Color(Eat()!.Value, 90);

    private string NextStringLiteral(int endColor = 0)
    {
        var (tokenKind, value, _) = Eat()!;
        var builder = new StringBuilder();
        for (var i = 0; i < value.Length; i++)
        {
            char? c = value[i];
            if (tokenKind == TokenKind.DoubleQuoteStringLiteral &&
                value.ElementAtOrDefault(i - 1) != '\\' &&
                c == '$' &&
                value.ElementAtOrDefault(i + 1) == '{')
            {
                i += 2;
                c = value.ElementAtOrDefault(i);
                builder.Append(Color("${", 37));

                var interpolationContentStart = i;
                var openBraces = 1;
                var inString = false;
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

                _innerHighlighter ??= new HighlightHandler(shell);

                if (value[i - 1] == '}')
                {
                    var interpolationContentEnd = i - 1;
                    builder.Append(
                        _innerHighlighter.Highlight(
                            value[interpolationContentStart..interpolationContentEnd],
                            _caret,
                            _unevaluatedVariables
                        )
                    );
                    builder.Append(Color("}", 37, endColor: 93));
                }
                else if (i <= value.Length && i != interpolationContentStart)
                {
                    builder.Append(
                        _innerHighlighter.Highlight(
                            value[interpolationContentStart..i],
                            _caret,
                            _unevaluatedVariables
                        )
                    );
                }
            }

            if (c.HasValue)
                builder.Append(c);
        }

        return Color(builder.ToString(), 93, endColor);
    }

    private string NextIdentifier(List<string>? modulePath = null)
    {
        var startIndex = Current!.Position.Index - 1;
        var identifier = Eat()!.Value.Trim();
        if (identifier.StartsWith('$'))
            return identifier;

        if (Current?.Kind == TokenKind.Equals)
        {
            Eat(); // =
            var assignmentValue = Current?.Kind is TokenKind.Identifier or null
                ? Eat()?.Value ?? ""
                : Next();

            return $"{Color(identifier, 36)}={Color(assignmentValue, 36)}";
        }

        var plurality = identifier.EndsWith('!') ? "!" : "";
        identifier = identifier.TrimEnd('!');
        modulePath ??= [];

        if (shell.StructExists(identifier))
            return Color(identifier + plurality, 96);

        if (!modulePath.Any() &&
            Current?.Kind != TokenKind.ColonColon &&
            (_unevaluatedVariables.Contains(identifier) || shell.VariableExists(identifier)))
            return identifier + plurality;

        modulePath.Add(identifier);
        if (shell.ModuleExists(modulePath))
            return NextModule(modulePath);

        modulePath.RemoveAt(modulePath.Count - 1);

        var (highlightedTextArguments, textArgumentsInfo) = NextTextArguments();
        if (Current?.Kind != TokenKind.OpenParenthesis || textArgumentsInfo.Arguments.Any())
        {
            var fullName = string.Join("::", modulePath.Append(identifier));
            _lastShellStyleInvocations.Add(
                new ShellStyleInvocationInfo(
                    fullName,
                    textArgumentsInfo,
                    // startIndex is the start index of the last identifier, but we need the start
                    // index of the entire qualified name.
                    startIndex - fullName.Length + identifier.Length,
                    Current?.Position.Index ?? _length
                )
            );
        }

        var isFunctionCall = shell.FunctionExists(identifier, modulePath);
        var isCallable = isFunctionCall ||
            modulePath.Count == 0 && (shell.ProgramExists(identifier) || shell.AliasExists(identifier));
        var colorCode = isCallable ? 95 : 91;

        var nextBuilder = new StringBuilder();
        if (Current?.Kind == TokenKind.WhiteSpace)
            nextBuilder.Append(Eat()!.Value);

        return highlightedTextArguments.Length > 0
            ? Color(identifier, colorCode, null) + plurality + highlightedTextArguments + nextBuilder
            : Color(identifier, colorCode) + plurality + nextBuilder;
    }

    private string NextClosure()
    {
        var builder = new StringBuilder();
        builder.Append(Eat()!.Value);

        // Gather all identifiers (parameters) after the `=>` and add them to
        // the _unevaluatedVariables hashset in order to keep track of closure
        // parameters that haven't been interpreted yet but should be highlighted.
        while (Current?.Kind is not (TokenKind.Ampersand or TokenKind.Colon or
               TokenKind.OpenBrace or TokenKind.EndOfFile or null))
        {
            var token = Eat()!;
            builder.Append(token.Value);
            if (token.Kind == TokenKind.Identifier)
                _unevaluatedVariables.Add(token.Value);
        }

        return builder.ToString();
    }

    private string NextModule(List<string> modulePath)
    {
        var builder = new StringBuilder();
        var name = Previous!.Value;
        if (!shell.ModuleExists(modulePath))
        {
            builder.Append(Color(name, 91));

            return builder.ToString();
        }

        builder.Append(Color(name, 34));
        if (Current is not { Kind: TokenKind.ColonColon })
            return builder.ToString();

        var pos = Current?.Position.Index ?? _length;
        var textForCompletion = string.Join("::", modulePath) + "::";
        _lastShellStyleInvocations.Add(
            new ShellStyleInvocationInfo(
                textForCompletion,
                new TextArgumentsInfo(Array.Empty<string>(), -1),
                pos - textForCompletion.Length,
                pos
            )
        );

        builder.Append(Eat()!.Value);

        if (Current != null)
            builder.Append(NextIdentifier(modulePath));

        return builder.ToString();
    }

    private string NextPath()
    {
        var startIndex = Current!.Position.Index - 1;
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

        var identifier = builder.ToString();
        var (highlightedTextArguments, textArgumentsInfo) = NextTextArguments();
        if (Current?.Kind != TokenKind.OpenParenthesis || textArgumentsInfo.Arguments.Any())
        {
            _lastShellStyleInvocations.Add(
                new ShellStyleInvocationInfo(
                    identifier,
                    textArgumentsInfo,
                    startIndex,
                    Current?.Position.Index ?? _length
                )
            );
        }

        var colorCode = shell.ProgramExists(identifier) ? 95 : 91;

        return highlightedTextArguments.Length > 0
            ? Color(identifier, colorCode, null) + highlightedTextArguments
            : Color(identifier, colorCode);
    }

    private (string highlighted, TextArgumentsInfo argumentsInfo) NextTextArguments()
    {
        if (Current?.Kind != TokenKind.WhiteSpace)
            return ("", new TextArgumentsInfo(Array.Empty<string>(), -1));

        var textArguments = new List<string>();
        var textArgumentBuilder = new StringBuilder();
        var caretAtArgumentIndex = -1;
        var startIndex = Current.Position.Index - 1;
        while (!ReachedTextEnd())
        {
            if (Current!.Kind is TokenKind.DoubleQuoteStringLiteral or TokenKind.SingleQuoteStringLiteral)
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
            else if (Current?.Kind == TokenKind.WhiteSpace)
            {
                var whiteSpace = Eat()!.Value;
                textArguments.Add(textArgumentBuilder.ToString());
                textArguments.Add(whiteSpace);
                textArgumentBuilder.Clear();

                // If the caret is within this text argument,
                // save the index of the argument.
                if (_caret >= startIndex && _caret <= Previous?.Position.Index + Previous?.Value.Length)
                    caretAtArgumentIndex = textArguments.Count - 1;
            }
            else
            {
                textArgumentBuilder.Append(Eat()!.Value);
            }
        }

        textArguments.Add(textArgumentBuilder.ToString());
        textArgumentBuilder.Clear();

        if (caretAtArgumentIndex == -1 && _caret >= startIndex)
            caretAtArgumentIndex = textArguments.Count - 1;

        var highlightedTextArguments = textArguments.Select(x =>
            FileUtils.IsValidStartOfPath(x, shell.WorkingDirectory)
                ? Underline(x)
                : x
        );

        return (
            Color(string.Concat(highlightedTextArguments), 36),
            new TextArgumentsInfo(textArguments, caretAtArgumentIndex)
        );
    }

    private string NextInterpolation(int endColor = 0)
    {
        var builder = new StringBuilder();
        Eat(); // $
        Eat(); // {
        builder.Append(Color("${", 37));

        var openBraces = 1;
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
                TokenKind.PipeErr or
                TokenKind.PipeAll or
                TokenKind.Semicolon or
                TokenKind.NewLine
            && Previous?.Kind != TokenKind.Backslash;
    }

    private static string Color(string text, int color, int? endColor = 0)
    {
        if (text.Length == 0)
            return "";

        return endColor == null
            ? $"\x1b[{color}m{text}"
            : $"\x1b[{color}m{text}\x1b[{endColor}m";
    }

    private static string Underline(string text)
        => $"\x1b[4m{text}\x1b[24m";

    private Token? Eat()
    {
        var token = Current;
        _index++;

        return token;
    }
}
