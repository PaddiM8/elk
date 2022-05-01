using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Elk.Interpreting;
using Elk.Interpreting.Scope;
using Elk.Lexing;

namespace Elk.Parsing;

internal class Parser
{
    private readonly List<Token> _tokens;
    private int _index;
    private Scope _scope;
    private readonly string _filePath;
    private bool _allowEndOfExpression;

    private Token? Current => _index < _tokens.Count
        ? _tokens[_index]
        : null;

    private Token? Previous => _index - 1 > 0
        ? _tokens[_index - 1]
        : null;

    private bool ReachedEnd => _index >= _tokens.Count;

    private Parser(List<Token> tokens, Scope scope, string filePath)
    {
        scope.GlobalScope.AddInclude(filePath);

        _tokens = tokens;
        _scope = scope;
        _filePath = filePath;
    }

    public static List<Expr> Parse(List<Token> tokens, Scope scope, string filePath)
    {
        var parser = new Parser(tokens, scope, filePath);
        var expressions = new List<Expr>();
        while (!parser.ReachedEnd)
        {
            if (parser.Match(TokenKind.Include))
            {
                var includedAst = parser.ParseInclude();
                if (includedAst != null)
                    expressions.AddRange(includedAst);
                
                continue;
            }

            var expr = parser.ParseExpr();
            expr.IsRoot = true;
            expressions.Add(expr);
            parser.SkipWhiteSpace();
        }

        return expressions;
    }

    private Expr ParseExpr()
    {
        if (Match(TokenKind.Fn))
        {
            return ParseFn();
        }
        
        if (Match(TokenKind.Return, TokenKind.Break, TokenKind.Continue))
        {
            return ParseKeywordExpr();
        }

        return ParseBinaryIf();
    }

    private Expr ParseFn()
    {
        EatExpected(TokenKind.Fn);
        var identifier = EatExpected(TokenKind.Identifier);

        var parameters = ParseParameterList();
        var functionScope = new LocalScope(_scope);
        foreach (var parameter in parameters)
        {
            functionScope.AddVariable(parameter.Identifier.Value, RuntimeNil.Value);
        }

        var block = ParseBlockOrSingle(StructureKind.Function, functionScope);
        var function = new FunctionExpr(identifier, parameters, block);

        _scope.GlobalScope.AddFunction(function);

        return function;
    }

    private Expr ParseKeywordExpr()
    {
        var keyword = Eat();
        _allowEndOfExpression = true;
        var value = ParseExpr();
        _allowEndOfExpression = false;

        return new KeywordExpr(
            keyword.Kind,
            value is EmptyExpr ? null : value,
            keyword.Position
        );
    }

    private List<Expr>? ParseInclude()
    {
        var pos = EatExpected(TokenKind.Include).Position;
        string relativePath = EatExpected(TokenKind.StringLiteral).Value;
        string directoryPath = Path.GetDirectoryName(_filePath)!;
        string absolutePath = Path.Combine(directoryPath, relativePath);

        if (_scope.GlobalScope.ContainsInclude(absolutePath))
            return null;
        
        if (!File.Exists(absolutePath))
        {
            throw new ParseException(pos, $"Cannot find file '{absolutePath}'");
        }

        _scope.GlobalScope.AddInclude(absolutePath);
        
        return Parse(
            Lexer.Lex(File.ReadAllText(absolutePath), absolutePath),
            _scope.GlobalScope,
            absolutePath
        );
    }

    private List<Parameter> ParseParameterList()
    {
        EatExpected(TokenKind.OpenParenthesis);
        var parameters = new List<Parameter>();

        do
        {
            if (Match(TokenKind.ClosedParenthesis))
                break;

            var identifier = EatExpected(TokenKind.Identifier);
            Expr? defaultValue = null;
            bool variadic = false;
            
            if (AdvanceIf(TokenKind.Equals))
                defaultValue = ParseExpr();
            
            if (AdvanceIf(TokenKind.DotDot))
            {
                EatExpected(TokenKind.Dot);
                variadic = true;
            }

            parameters.Add(new Parameter(identifier, defaultValue, variadic));
        }
        while(AdvanceIf(TokenKind.Comma));

        EatExpected(TokenKind.ClosedParenthesis);

        return parameters;
    }

    private Expr ParseBinaryIf()
    {
        int leftLine = Current?.Position.Line ?? 0;
        var left = ParseAssignment();
        if (leftLine == Current?.Position.Line && Match(TokenKind.If))
        {
            var op = Eat();
            var right = ParseAssignment();

            return new BinaryExpr(left, op.Kind, right);
        }

        return left;
    }

    private Expr ParseAssignment()
    {
        var left = ParsePipe();

        if (Match(TokenKind.Equals,
            TokenKind.PlusEquals,
            TokenKind.MinusEquals,
            TokenKind.StarEquals,
            TokenKind.SlashEquals))
        {
            var op = Eat();
            var right = ParsePipe();

            return op.Kind switch
            {
                TokenKind.Equals => new BinaryExpr(left, TokenKind.Equals, right),
                TokenKind.PlusEquals => new BinaryExpr(
                    left,
                    TokenKind.Equals,
                    new BinaryExpr(left, TokenKind.Plus, right)
                ),
                TokenKind.MinusEquals => new BinaryExpr(
                    left,
                    TokenKind.Equals,
                    new BinaryExpr(left, TokenKind.Minus, right)
                ),
                TokenKind.StarEquals => new BinaryExpr(
                    left,
                    TokenKind.Equals,
                    new BinaryExpr(left, TokenKind.Star, right)
                ),
                TokenKind.SlashEquals => new BinaryExpr(
                    left,
                    TokenKind.Equals,
                    new BinaryExpr(left, TokenKind.Slash, right)
                ),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        return left;
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
            bool inclusive = AdvanceIf(TokenKind.Equals);

            return new RangeExpr(null, ParseCoalescing(), inclusive);
        }

        var left = ParseCoalescing();
        if (Peek()?.Kind is not TokenKind.Slash && AdvanceIf(TokenKind.DotDot))
        {
            bool inclusive = AdvanceIf(TokenKind.Equals);

            bool allowedEnd = _allowEndOfExpression;
            _allowEndOfExpression = true;
            var right = ParseCoalescing();
            _allowEndOfExpression = allowedEnd;

            return right is EmptyExpr
                ? new RangeExpr(left, null, false)
                : new RangeExpr(left, right, inclusive);
        }

        return left;
    }

    private Expr ParseCoalescing()
    {
        var left = ParseAdditive();

        while (Match(TokenKind.QuestionQuestion))
        {
            var op = Eat().Kind;
            var right = ParseAdditive();

            left = new BinaryExpr(left, op, right);
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
        var left = ParseExponent();

        while (Match(TokenKind.Star, TokenKind.Slash, TokenKind.Percent))
        {
            var op = Eat().Kind;
            var right = ParseExponent();

            left = new BinaryExpr(left, op, right);
        }

        return left;
    }

    private Expr ParseExponent()
    {
        var left = ParseUnary();

        if (Match(TokenKind.Caret))
        {
            Eat();

            return new BinaryExpr(left, TokenKind.Caret, ParseExponent());
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
                TokenKind.StringLiteral => ParseStringLiteral(),
                _ => new LiteralExpr(Eat()),
            };
        }
        
        if (Match(TokenKind.OpenParenthesis))
        {
            return ParseParenthesis();
        }
        
        if (Match(TokenKind.Let))
        {
            return ParseLet();
        }
        
        if (Match(TokenKind.If))
        {
            return ParseIf();
        }
        
        if (Match(TokenKind.For))
        {
            return ParseFor();
        }
        
        if (Match(TokenKind.OpenSquareBracket))
        {
            return ParseList();
        }
        
        if (Match(TokenKind.OpenBrace))
        {
            // Go forward a bit in order to be able to look ahead,
            // but make sure to make it possible to go back again afterwards.
            int prevIndex = _index;
            Eat();
            SkipWhiteSpace();

            if (Match(TokenKind.Identifier, TokenKind.StringLiteral) &&
                Peek()?.Kind == TokenKind.Colon)
            {
                _index = prevIndex;

                return ParseDictionary();
            }

            _index = prevIndex;

            return ParseBlock(StructureKind.Other);
        }
        
        if (Match(TokenKind.Identifier, TokenKind.Dot, TokenKind.DotDot, TokenKind.Slash, TokenKind.Tilde))
        {
            return ParseIdentifier();
        }

        if (_allowEndOfExpression)
        {
            return new EmptyExpr();
        }
        
        throw Current == null
            ? Error("Unexpected end of expression")
            : Error($"Unexpected token: '{Current?.Kind}'");
    }

    private Expr ParseStringLiteral()
    {
        var stringLiteral = EatExpected(TokenKind.StringLiteral);
        var parts = StringInterpolationParser.Parse(stringLiteral);
        var parsedParts = new List<Expr>();
        int column = stringLiteral.Position.Column;
        foreach (var part in parts)
        {
            var textPos = new TextPos(column, stringLiteral.Position.Line, _filePath);
            if (part.Kind == InterpolationPartKind.Text)
            {
                var token = new Token(
                    TokenKind.StringLiteral,
                    part.Value,
                    textPos
                );
                parsedParts.Add(new LiteralExpr(token));
            }
            else
            {
                var tokens = Lexer.Lex(part.Value, textPos);
                var ast = Parse(tokens, _scope, _filePath);
                if (ast.Count != 1)
                    throw new ParseException(textPos, "Expected exactly one expression in the string interpolation block");
                
                parsedParts.Add(ast.First());
            }

            column += part.Value.Length;
        }

        return new StringInterpolationExpr(parsedParts, stringLiteral.Position);
    }

    private Expr ParseParenthesis()
    {
        var pos = EatExpected(TokenKind.OpenParenthesis).Position;
        var expressions = new List<Expr>();
        do
        {
            expressions.Add(ParseExpr());
        }
        while (AdvanceIf(TokenKind.Comma) && !Match(TokenKind.ClosedParenthesis));

        EatExpected(TokenKind.ClosedParenthesis);

        return expressions.Count == 1
            ? expressions.First()
            : new TupleExpr(expressions, pos);
    }

    private Expr ParseLet()
    {
        EatExpected(TokenKind.Let);

        var identifierList = ParseIdentifierList();
        EatExpected(TokenKind.Equals);
        foreach (var identifier in identifierList)
            _scope.AddVariable(identifier.Value, RuntimeNil.Value);

        return new LetExpr(identifierList, ParseExpr());
    }

    private Expr ParseIf()
    {
        EatExpected(TokenKind.If);

        var condition = ParseExpr();
        var thenBranch = ParseBlockOrSingle(StructureKind.Other);
        var elseBranch = AdvanceIf(TokenKind.Else)
            ? ParseExpr()
            : null;
        
        return new IfExpr(condition, thenBranch, elseBranch);
    }

    private Expr ParseFor()
    {
        EatExpected(TokenKind.For);
        var identifierList = ParseIdentifierList();
        EatExpected(TokenKind.In);
        var value = ParseExpr();

        var scope = new LocalScope(_scope);
        foreach (var identifier in identifierList)
            _scope.AddVariable(identifier.Value, RuntimeNil.Value);
        
        var branch = ParseBlockOrSingle(StructureKind.Loop, scope);

        return new ForExpr(identifierList, value, branch);
    }

    private List<Token> ParseIdentifierList()
    {
        if (Match(TokenKind.Identifier))
            return new() { Eat() };

        EatExpected(TokenKind.OpenParenthesis);

        var identifiers = new List<Token>();
        do
        {
            identifiers.Add(EatExpected(TokenKind.Identifier));
        }
        while (AdvanceIf(TokenKind.Comma));
        
        EatExpected(TokenKind.ClosedParenthesis);

        return identifiers;
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

    private Expr ParseDictionary()
    {
        var pos = EatExpected(TokenKind.OpenBrace).Position;
        var entries = new List<(string, Expr)>();
        while (Match(TokenKind.Identifier, TokenKind.StringLiteral))
        {
            var identifier = Eat().Value;
            EatExpected(TokenKind.Colon);
            var value = ParseExpr();
            entries.Add((identifier, value));

            if (!Match(TokenKind.ClosedBrace))
                EatExpected(TokenKind.Comma);
        }

        AdvanceIf(TokenKind.Comma);
        EatExpected(TokenKind.ClosedBrace);

        return new DictionaryExpr(entries, pos);
    }

    private BlockExpr ParseBlockOrSingle(StructureKind parentStructureKind, LocalScope? scope = null)
    {
        if (AdvanceIf(TokenKind.Colon))
        {
            _scope = scope ?? new LocalScope(_scope);
            var expr = ParseExpr();
            _scope = _scope.Parent!;

            return new BlockExpr(
                new() { expr },
                parentStructureKind,
                expr.Position
            );
        }

        return ParseBlock(parentStructureKind, scope);
    }

    private BlockExpr ParseBlock(StructureKind parentStructureKind, LocalScope? scope = null)
    {
        EatExpected(TokenKind.OpenBrace);

        var pos = Current!.Position;
        _scope = scope ?? new LocalScope(_scope);

        var expressions = new List<Expr>();
        while (!AdvanceIf(TokenKind.ClosedBrace))
            expressions.Add(ParseExpr());

        _scope = _scope.Parent!;

        return new BlockExpr(expressions, parentStructureKind, pos);
    }

    private Expr ParseIdentifier()
    {
        var pos = Current?.Position ?? TextPos.Default;
        var identifier = Match(TokenKind.Identifier)
            ? Eat()
            : new Token(TokenKind.Identifier, ParsePath(), pos);
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

            return new CallExpr(identifier, arguments, CallStyle.Parenthesized);
        }
        
        if (identifier.Value.StartsWith('$') || _scope.ContainsVariable(identifier.Value))
        {
            return new VariableExpr(identifier);
        }
        
        var textArguments = new List<Expr>();
        var currentText = new StringBuilder();
        AdvanceIf(TokenKind.WhiteSpace);

        while (!ReachedTextEnd())
        {
            if (AdvanceIf(TokenKind.WhiteSpace))
            {
                var token = new Token(
                    TokenKind.StringLiteral,
                    currentText.ToString(),
                    identifier.Position
                );
                textArguments.Add(new LiteralExpr(token));
                currentText.Clear();
                continue;
            }

            var next = Peek();
            if (MatchInclWhiteSpace(TokenKind.Tilde) &&
                (next == null || next.Kind is TokenKind.Slash or TokenKind.WhiteSpace))
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
            var finalToken = new Token(
                TokenKind.StringLiteral,
                currentText.ToString(),
                identifier.Position
            );
            textArguments.Add(new LiteralExpr(finalToken));
        }

        return new CallExpr(identifier, textArguments, CallStyle.TextArguments);
    }

    private string ParsePath()
    {
        var value = new StringBuilder();
        while (!ReachedTextEnd() &&
               !MatchInclWhiteSpace(TokenKind.WhiteSpace, TokenKind.OpenParenthesis, TokenKind.OpenSquareBracket))
        {
            // If ".." is not before/after a slash, it is not a part of a path
            // and the loop should be stopped.
            if (MatchInclWhiteSpace(TokenKind.DotDot) &&
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
            TokenKind.OpenBrace,
            TokenKind.ClosedBrace,
            TokenKind.Pipe,
            TokenKind.And,
            TokenKind.Or,
            TokenKind.NewLine
        );
    }

    private bool AdvanceIf(TokenKind kind)
    {
        int prevIndex = _index;

        if (Match(kind))
        {
            Eat();
            return true;
        }

        _index = prevIndex;

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
            : new(Current?.Position ?? TextPos.Default, message);

    private void SkipWhiteSpace()
    {
        while (Current?.Kind is TokenKind.WhiteSpace or TokenKind.NewLine or TokenKind.Comment)
            Eat();
    }
}