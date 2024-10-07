using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Lexing;
using Elk.Scoping;

namespace Elk.Services;

public record ShellStyleInvocationInfo(
    string Name,
    TextArgumentsInfo TextArgumentsInfo,
    int StartIndex,
    int EndIndex);

public record TextArgumentsInfo(
    IList<string> Arguments,
    int ActiveArgumentIndex,
    int StartIndex
);

public class Highlighter(ModuleScope module, ShellSession? shell)
{
    public IEnumerable<ShellStyleInvocationInfo> LastShellStyleInvocations
        => _lastShellStyleInvocations;

    private Token? Current
        => _tokens.ElementAtOrDefault(_index);

    private Token? Previous
        => _tokens.ElementAtOrDefault(_index - 1);

    private bool ReachedEnd
        => _index >= _tokens.Count;

    private List<Token> _tokens = null!;
    private readonly List<SemanticToken> _semanticTokens = [];
    private int _index;
    private int _length;
    private int _caret;
    private Highlighter? _innerHighlighter;
    private readonly List<ShellStyleInvocationInfo> _lastShellStyleInvocations = [];
    private readonly HashSet<string> _unevaluatedVariables = [];

    public IEnumerable<SemanticToken> Highlight(string text, int caret)
        => Highlight(text, null, caret, null);

    private IEnumerable<SemanticToken> Highlight(
        string text,
        TextPos? startPos,
        int caret,
        HashSet<string>? unevaluatedVariables)
    {
        startPos ??= new TextPos(1, 1, 0, "");
        _tokens = Lexer.Lex(text, startPos, out _, LexerMode.Preserve);
        _semanticTokens.Clear();
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

        while (!ReachedEnd)
            Next();

        return _semanticTokens;
    }

    private void Next()
    {
        if (Current?.Kind is TokenKind.Dot or TokenKind.DotDot or TokenKind.Tilde && Peek()?.Kind == TokenKind.Slash)
        {
            NextPath();

            return;
        }

        if (Current?.Kind == TokenKind.Slash && Peek()?.Kind == TokenKind.Identifier)
        {
            NextPath();

            return;
        }

        if (Current is { Kind: TokenKind.Identifier, Value.Length: 1 } && Peek()?.Kind == TokenKind.Colon && Peek(2)?.Kind == TokenKind.Slash)
        {
            NextPath();

            return;
        }

        switch (Current?.Kind)
        {
            case >= TokenKind.Not and <= TokenKind.Pub:
                NextKeyword();
                break;
            case TokenKind.IntegerLiteral:
            case TokenKind.FloatLiteral:
                NextNumberLiteral();
                break;
            case TokenKind.Comment:
                NextComment();
                break;
            case TokenKind.DoubleQuoteStringLiteral:
            case TokenKind.SingleQuoteStringLiteral:
                NextStringLiteral();
                break;
            case TokenKind.Identifier:
                NextIdentifier();
                break;
            case TokenKind.Arrow:
                NextFieldAccess();
                break;
            case TokenKind.EqualsGreater:
                NextClosure();
                break;
            default:
                Push(Eat()!);
                break;
        }
    }

    private void NextKeyword()
    {
        var keyword = Eat()!;
        Push(SemanticTokenKind.Keyword, keyword);

        if (keyword.Kind == TokenKind.Fn)
        {
            var reachedFirstIdentifier = false;
            while (!ReachedEnd && Current?.Kind is not TokenKind.OpenBrace and not TokenKind.Colon)
            {
                var token = Eat()!;
                if (token.Kind == TokenKind.Identifier)
                {
                    if (!reachedFirstIdentifier)
                    {
                        reachedFirstIdentifier = true;
                        Push(SemanticTokenKind.Function, token);

                        continue;
                    }

                    _unevaluatedVariables.Add(token.Value);
                    Push(SemanticTokenKind.Parameter, token);
                }
                else
                {
                    Push(token);
                }
            }
        }
        else if (keyword.Kind is TokenKind.Let or TokenKind.Alias)
        {
            while (!ReachedEnd && Current?.Kind is not TokenKind.Equals)
            {
                var token = Eat()!;
                if (token.Kind == TokenKind.Identifier)
                {
                    _unevaluatedVariables.Add(token.Value);
                    Push(SemanticTokenKind.Variable, token);
                }
                else
                {
                    Push(token);
                }
            }
        }
        else if (keyword.Kind == TokenKind.For)
        {
            while (!ReachedEnd && Current?.Kind is not TokenKind.In)
            {
                var token = Eat()!;
                if (token.Kind == TokenKind.Identifier)
                {
                    _unevaluatedVariables.Add(token.Value);
                    Push(SemanticTokenKind.Variable, token);
                }
                else
                {
                    Push(token);
                }
            }
        }
        else if (keyword.Kind == TokenKind.With)
        {
            while (!ReachedEnd && Current?.Kind != TokenKind.NewLine && Current?.Value != "from")
                Push(Eat()!);

            if (Current?.Value == "from")
            {
                Push(SemanticTokenKind.Keyword, Eat()!);
                if (Current?.Kind == TokenKind.WhiteSpace)
                    Push(Eat()!);
                if (Current?.Kind == TokenKind.Identifier)
                    Push(Eat()!);
            }
        }
        else if (keyword.Kind is TokenKind.Using or TokenKind.Unalias or TokenKind.Module)
        {
            if (Current?.Kind == TokenKind.WhiteSpace)
                Push(Eat()!);
            if (Current?.Kind == TokenKind.Identifier)
                Push(Eat()!);
        }
        else if (keyword.Kind == TokenKind.Struct)
        {
            if (Current?.Kind == TokenKind.WhiteSpace)
                Push(Eat()!);
            if (Current?.Kind == TokenKind.Identifier)
            {
                _unevaluatedVariables.Add(Current.Value);
                Push(SemanticTokenKind.Struct, Eat()!);
            }

            while (!ReachedEnd && Current?.Kind != TokenKind.ClosedParenthesis)
                Push(Eat()!);
        }
        else if (keyword.Kind == TokenKind.Catch)
        {
            if (Current?.Kind == TokenKind.WhiteSpace)
                Push(Eat()!);
            if (Current?.Kind == TokenKind.Identifier)
                _unevaluatedVariables.Add(Current.Value);
        }
    }

    private void NextNumberLiteral()
    {
        Push(SemanticTokenKind.Number, Eat()!);
    }

    private void NextComment()
    {
        Push(SemanticTokenKind.Comment, Eat()!);
    }

    private void NextStringLiteral()
    {
        var (tokenKind, value, position) = Eat()!;
        var part = new StringBuilder();
        var partOffset = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];

            var isEnvironmentVariable = tokenKind == TokenKind.DoubleQuoteStringLiteral &&
                value.ElementAtOrDefault(i - 1) != '\\' &&
                c == '$' &&
                IsEnvironmentVariableCharacter(value.ElementAtOrDefault(i + 1));
            if (isEnvironmentVariable)
            {
                Push(
                    SemanticTokenKind.String,
                    part.ToString(),
                    position with
                    {
                        Index = position.Index + partOffset,
                        Column = position.Column + partOffset,
                    }
                );
                part.Clear();
                part.Append(c);
                i++;

                while (IsEnvironmentVariableCharacter(value.ElementAtOrDefault(i)))
                {
                    part.Append(value[i]);
                    i++;
                }

                Push(
                    SemanticTokenKind.Variable,
                    part.ToString(),
                    position with
                    {
                        Index = position.Index + partOffset,
                        Column = position.Column + partOffset,
                    }
                );
                part.Clear();
                i--;

                continue;
            }

            var isInterpolation = tokenKind == TokenKind.DoubleQuoteStringLiteral &&
                value.ElementAtOrDefault(i - 1) != '\\' &&
                c == '$' &&
                value.ElementAtOrDefault(i + 1) == '{';
            if (!isInterpolation)
            {
                part.Append(c);

                continue;
            }

            Push(
                SemanticTokenKind.String,
                part.ToString(),
                position with
                {
                    Index = position.Index + partOffset,
                    Column = position.Column + partOffset,
                }
            );
            part.Clear();

            Push(
                SemanticTokenKind.InterpolationOperator,
                "${",
                position with
                {
                    Index = position.Index + i,
                    Column = position.Column + i,
                }
            );
            i += 2;
            c = value.ElementAtOrDefault(i);

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

            _innerHighlighter ??= new Highlighter(module, shell);

            if (value[i - 1] == '}')
            {
                partOffset = i;
                var interpolationContentEnd = i - 1;
                _semanticTokens.AddRange(
                    _innerHighlighter.Highlight(
                        value[interpolationContentStart..interpolationContentEnd],
                        position with
                        {
                            Index = position.Index + interpolationContentStart,
                            Column = position.Column + interpolationContentStart,
                        },
                        _caret,
                        _unevaluatedVariables
                    )
                );
                Push(
                    SemanticTokenKind.InterpolationOperator,
                    "}",
                    position with
                    {
                        Index = position.Index + interpolationContentEnd,
                        Column = position.Column + interpolationContentEnd,
                    }
                );
                part.Append(c);
            }
            else if (i <= value.Length && i != interpolationContentStart)
            {
                _semanticTokens.AddRange(
                    _innerHighlighter.Highlight(
                        value[interpolationContentStart..i],
                        position with
                        {
                            Index = position.Index + interpolationContentStart - 1,
                            Column = position.Column + interpolationContentStart - 1,
                        },
                        _caret,
                        _unevaluatedVariables
                    )
                );
            }
        }

        if (part.Length == 0)
            return;

        Push(
            SemanticTokenKind.String,
            part.ToString(),
            position with
            {
                Index = position.Index + partOffset,
                Column = position.Column + partOffset,
            }
        );
    }

    private bool IsEnvironmentVariableCharacter(char c)
        => char.IsLetterOrDigit(c) || c == '_';

    private void NextIdentifier(List<string>? modulePath = null)
    {
        var startIndex = Current!.Position.Index - 1;
        var identifierToken = Eat()!;
        var identifier = identifierToken.Value.Trim();
        if (identifier.StartsWith('$'))
        {
            Push(identifierToken);

            return;
        }

        if (Current?.Kind == TokenKind.Equals)
        {
            Push(SemanticTokenKind.Variable, identifierToken);
            Push(Eat()!); // =
            if (Current?.Kind is TokenKind.Identifier)
            {
                Push(SemanticTokenKind.String, Eat()!);
            }
            else if (Current != null)
            {
                Next();
            }

            return;
        }

        modulePath ??= [];
        if (module.StructExists(identifier))
        {
            Push(SemanticTokenKind.Struct, identifierToken);

            return;
        }

        if (!modulePath.Any() &&
            Current?.Kind != TokenKind.ColonColon &&
            (_unevaluatedVariables.Contains(identifier) || module.VariableExists(identifier)))
        {
            Push(SemanticTokenKind.Variable, identifierToken);

            return;
        }

        modulePath.Add(identifier);
        if (module.ModuleExists(modulePath) && Current?.Kind != TokenKind.WhiteSpace)
        {
            NextModule(modulePath);

            return;
        }

        modulePath.RemoveAt(modulePath.Count - 1);

        var isFunctionCall = module.FunctionExists(identifier, modulePath);
        var isCallable = isFunctionCall ||
            modulePath.Count == 0 && (shell?.ProgramExists(identifier) is true || module.AliasExists(identifier));
        Push(
            isCallable
                ? SemanticTokenKind.Function
                : SemanticTokenKind.UnknownSymbol,
            identifierToken
        );

        var textArgumentsInfo = NextTextArguments();
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

        if (Current?.Kind == TokenKind.WhiteSpace)
            Push(Eat()!);
    }

    private void NextClosure()
    {
        Push(Eat()!);

        // Gather all identifiers (parameters) after the `=>` and add them to
        // the _unevaluatedVariables hashset in order to keep track of closure
        // parameters that haven't been interpreted yet but should be highlighted.
        while (Current?.Kind is not (TokenKind.Ampersand or TokenKind.Colon or
               TokenKind.OpenBrace or TokenKind.EndOfFile or null))
        {
            var token = Eat()!;
            Push(SemanticTokenKind.Variable, token);
            if (token.Kind == TokenKind.Identifier)
                _unevaluatedVariables.Add(token.Value);
        }
    }

    private void NextModule(List<string> modulePath)
    {
        var name = Previous!;
        if (!module.ModuleExists(modulePath))
        {
            Push(SemanticTokenKind.UnknownSymbol, name);

            return;
        }

        Push(SemanticTokenKind.Module, name);
        if (Current is not { Kind: TokenKind.ColonColon })
            return;

        var pos = Current?.Position.Index ?? _length;
        var textForCompletion = string.Join("::", modulePath) + "::";
        _lastShellStyleInvocations.Add(
            new ShellStyleInvocationInfo(
                textForCompletion,
                new TextArgumentsInfo(Array.Empty<string>(), -1, -1),
                pos - textForCompletion.Length,
                pos
            )
        );

        Push(Eat()!);

        if (Current != null)
            NextIdentifier(modulePath);
    }

    private void NextPath()
    {
        var position = Current!.Position;
        var identifierBuilder = new StringBuilder();
        while (!ReachedTextEnd() &&
               Current?.Kind is not (TokenKind.WhiteSpace or TokenKind.OpenParenthesis or TokenKind.OpenSquareBracket))
        {
            // If ".." is not before/after a slash, it is not a part of a path
            // and the loop should be stopped.
            if (Current?.Kind == TokenKind.DotDot &&
                Previous?.Kind != TokenKind.Slash &&
                Peek()?.Kind != TokenKind.Slash)
            {
                break;
            }

            identifierBuilder.Append(Eat()!.Value);
        }

        var identifier = identifierBuilder.ToString();
        var kind = shell?.ProgramExists(identifier) is true
            ? SemanticTokenKind.Function
            : SemanticTokenKind.UnknownSymbol;
        Push(kind, identifier, position);

        var textArgumentsInfo = NextTextArguments();
        if (Current?.Kind != TokenKind.OpenParenthesis || textArgumentsInfo.Arguments.Any())
        {
            _lastShellStyleInvocations.Add(
                new ShellStyleInvocationInfo(
                    identifier,
                    textArgumentsInfo,
                    position.Index - 1,
                    Current?.Position.Index ?? _length
                )
            );
        }
    }

    private TextArgumentsInfo NextTextArguments()
    {
        if (Current?.Kind != TokenKind.WhiteSpace)
            return new TextArgumentsInfo(Array.Empty<string>(), -1, -1);

        var textArguments = new List<string>();
        var caretAtArgumentIndex = -1;
        var textArgumentTokens = new List<Token>();
        var startIndex = Current.Position.Index - 1;
        int? initialWhiteSpaceLength = null;

        void AppendTextArgumentTokens()
        {
            var argumentString = string.Join("", textArgumentTokens.Select(x => x.Value));
            if (textArgumentTokens.Any())
            {
                var unescaped = Utils.Unescape(argumentString);
                var kind = shell != null && FileUtils.IsValidStartOfPath(unescaped, shell.WorkingDirectory)
                    ? SemanticTokenKind.Path
                    : SemanticTokenKind.TextArgument;
                Push(kind, argumentString, textArgumentTokens.First().Position);
                textArgumentTokens.Clear();
            }
        }

        while (!ReachedTextEnd())
        {
            if (Current!.Kind is TokenKind.DoubleQuoteStringLiteral or TokenKind.SingleQuoteStringLiteral)
            {
                AppendTextArgumentTokens();
                NextStringLiteral();
            }
            else if (Previous?.Kind != TokenKind.Backslash &&
                 Current!.Value == "$" &&
                 Peek()?.Kind == TokenKind.OpenBrace)
            {
                AppendTextArgumentTokens();
                NextInterpolation();
            }
            else if (Current?.Kind == TokenKind.Backslash)
            {
                textArgumentTokens.Add(Eat()!);
                if (Current != null)
                    textArgumentTokens.Add(Eat()!);
            }
            else if (Current?.Kind == TokenKind.WhiteSpace)
            {
                AppendTextArgumentTokens();

                var whiteSpaceLength = 0;
                while (Current?.Kind == TokenKind.WhiteSpace)
                {
                    Push(Eat()!); // Whitespace
                    whiteSpaceLength++;
                }

                initialWhiteSpaceLength ??= whiteSpaceLength;

                // If the caret is within this text argument,
                // save the index of the argument.
                if (_caret >= startIndex && _caret <= Previous?.Position.Index + Previous?.Value.Length)
                    caretAtArgumentIndex = textArguments.Count - 1;
            }
            else
            {
                textArgumentTokens.Add(Eat()!);
            }
        }

        var lastArgumentString = string.Join("", textArgumentTokens.Select(x => x.Value));
        if (textArgumentTokens.Any())
        {
            var unescaped = Utils.Unescape(lastArgumentString);
            var kind = shell != null && FileUtils.IsValidStartOfPath(unescaped, shell.WorkingDirectory)
                ? SemanticTokenKind.Path
                : SemanticTokenKind.TextArgument;
            Push(kind, lastArgumentString, textArgumentTokens.First().Position);
        }

        textArgumentTokens.Clear();

        if (caretAtArgumentIndex == -1 && _caret >= startIndex)
            caretAtArgumentIndex = textArguments.Count - 1;

        return new TextArgumentsInfo(
            textArguments,
            caretAtArgumentIndex,
            startIndex + (initialWhiteSpaceLength ?? 0) + 1
        );
    }

    private void NextInterpolation()
    {
        Push(SemanticTokenKind.InterpolationOperator, Eat()!); // $
        Push(SemanticTokenKind.InterpolationOperator, Eat()!); // {

        var openBraces = 1;
        while (!ReachedEnd && openBraces > 0)
        {
            if (Current?.Kind == TokenKind.OpenBrace)
                openBraces++;
            else if (Current?.Kind == TokenKind.ClosedBrace)
                openBraces--;

            if (openBraces == 0)
            {
                Push(SemanticTokenKind.InterpolationOperator, Eat()!); // \
                break;
            }

            Next();
        }
    }

    private void NextFieldAccess()
    {
        Push(Eat()!); // ->
        if (Current?.Kind == TokenKind.Identifier)
            Push(SemanticTokenKind.Variable, Eat()!);
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

    private Token? Eat()
    {
        var token = Current;
        _index++;

        return token;
    }

    private Token? Peek(int count = 1)
        => _tokens.ElementAtOrDefault(_index + count);

    private void Push(Token token)
    {
        _semanticTokens.Add(
            new SemanticToken(SemanticTokenKind.None, token.Value, token.Position)
        );
    }

    private void Push(SemanticTokenKind kind, Token token)
    {
        _semanticTokens.Add(
            new SemanticToken(kind, token.Value, token.Position)
        );
    }

    private void Push(SemanticTokenKind kind, string value, TextPos position)
    {
        _semanticTokens.Add(
            new SemanticToken(kind, value, position)
        );
    }
}
