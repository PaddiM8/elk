#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Elk.Interpreting;
using Elk.Interpreting.Exceptions;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Std.Bindings;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Parsing;

internal class Parser
{
    private Token? Current
        => _index < _tokens.Count
            ? _tokens[_index]
            : null;

    private Token? Previous
        => _index - 1 > 0
            ? _tokens[_index - 1]
            : null;

    private bool ReachedEnd
        => _index >= _tokens.Count;

    private bool UseAliases
        => Current?.Position.FilePath == null;

    private bool _allowEndOfExpression;
    private int _index;
    private Scope _scope;
    private readonly List<Token> _tokens;

    private Parser(
        List<Token> tokens,
        Scope scope)
    {
        _tokens = tokens;
        _scope = scope;
    }

    public static Ast Parse(
        List<Token> tokens,
        Scope scope)
    {
        var parser = new Parser(
            tokens,
            scope
        );
        var expressions = new List<Expr>();
        while (!parser.ReachedEnd)
        {
            if (parser.Match(TokenKind.With))
            {
                parser.ParseWith();

                continue;
            }

            if (parser.Match(TokenKind.Using))
            {
                parser.ParseUsing();

                continue;
            }

            while (parser.Match(TokenKind.Comment))
                parser.Eat();

            if (parser.ReachedEnd)
                break;

            var expr = parser.ParseExprOrDecl();
            expr.IsRoot = true;
            expressions.Add(expr);
            parser.SkipWhiteSpace();
        }

        return new Ast(expressions);
    }

    private void ParseWith()
    {
        var pos = EatExpected(TokenKind.With).Position;

        // Peek ahead to check if there is a 'from', then backtrack
        SkipWhiteSpace();
        var prevIndex = _index;
        var hasFrom = Eat().Kind == TokenKind.Identifier &&
            (Match(TokenKind.Comma) || MatchIdentifier("from"));
        _index = prevIndex;

        // Avoid reserving the keyword
        var symbolImportTokens = new List<Token>();
        if (hasFrom)
        {
            do
            {
                symbolImportTokens.Add(EatExpected(TokenKind.Identifier));
            }
            while (AdvanceIf(TokenKind.Comma));

            if (!MatchIdentifier("from"))
                throw Error("Expected 'from'");

            Eat();
        }

        SkipWhiteSpace();
        var relativePath = ParsePath();
        var moduleName = Path.GetFileNameWithoutExtension(relativePath);

        if (StdBindings.HasModule(moduleName))
        {
            foreach (var symbolImportToken in symbolImportTokens)
            {
                if (StdBindings.HasFunction(symbolImportToken.Value, moduleName))
                {
                    throw new RuntimeException(
                        $"Module does not contain function '{symbolImportToken.Value}'",
                        pos,
                        Previous!.EndPosition
                    );
                }

                _scope.ModuleScope.ImportStdFunction(symbolImportToken.Value, moduleName);
            }

            return;
        }

        var importScope = ImportUserModule(relativePath, moduleName, pos);
        foreach (var symbolImportToken in symbolImportTokens)
        {
            _scope.ModuleScope.ImportUnknown(importScope, symbolImportToken);
        }
    }

    private void ParseUsing()
    {
        var pos = EatExpected(TokenKind.Using).Position;
        SkipWhiteSpace();
        var relativePath = ParsePath();
        var moduleName = Path.GetFileNameWithoutExtension(relativePath);

        var isStdModule = StdBindings.GetModuleSymbolNames(
            moduleName,
            out var stdStructNames,
            out var stdFunctionNames
        );
        if (isStdModule)
        {
            foreach (var stdStructName in stdStructNames)
                _scope.ModuleScope.ImportStdStruct(stdStructName, moduleName);

            foreach (var stdFunctionName in stdFunctionNames)
                _scope.ModuleScope.ImportStdFunction(stdFunctionName, moduleName);

            return;
        }

        var importScope = ImportUserModule(relativePath, moduleName, pos);
        foreach (var symbol in importScope.Functions.Where(x => x.Expr.AccessLevel == AccessLevel.Public))
            _scope.ModuleScope.ImportFunction(symbol);

        foreach (var symbol in importScope.Structs.Where(x => x.Expr?.AccessLevel == AccessLevel.Public))
            _scope.ModuleScope.ImportStruct(symbol);

        foreach (var module in importScope.Modules.Where(x => x is { Name: not null, AccessLevel: AccessLevel.Public }))
            _scope.ModuleScope.ImportModule(module.Name!, module);
    }

    private ModuleScope ImportUserModule(string path, string moduleName, TextPos pos)
    {
        var filePath = Previous?.Position.FilePath ?? _scope.ModuleScope.FilePath;
        var directoryPath = filePath == null
            ? ShellEnvironment.WorkingDirectory
            : Path.GetDirectoryName(filePath)!;
        var absolutePath = Path.GetFullPath(Path.Combine(directoryPath, path) + ".elk");

        if (!File.Exists(absolutePath))
        {
            throw new RuntimeException(
                $"Cannot find file '{absolutePath}'",
                pos,
                Previous!.EndPosition
            );
        }

        var importScope = _scope.ModuleScope.RootModule.FindRegisteredModule(absolutePath);
        if (importScope == null)
        {
            importScope = ModuleScope.CreateAsImported(
                AccessLevel.Public,
                moduleName,
                _scope.ModuleScope.RootModule,
                absolutePath,
                new Ast(Array.Empty<Expr>())
            );
            _scope.ModuleScope.RootModule.RegisterModule(absolutePath, importScope);

            importScope.Ast = Parse(
                Lexer.Lex(File.ReadAllText(absolutePath), absolutePath, out var lexError),
                importScope
            );
            if (lexError != null)
                throw lexError;
        }

        _scope.ModuleScope.ImportModule(moduleName, importScope);

        return importScope;
    }

    private Expr ParseExprOrDecl()
    {
        SkipWhiteSpace();

        while (AdvanceIf(TokenKind.Semicolon))
            SkipWhiteSpace();

        if (AdvanceIf(TokenKind.Pub))
        {
            SkipWhiteSpace();

            return Current?.Kind switch
            {
                TokenKind.Module => ParseModule(AccessLevel.Public),
                TokenKind.Struct => ParseStruct(AccessLevel.Public),
                TokenKind.Fn => ParseFn(AccessLevel.Public),
                _ => throw new RuntimeException(
                    "Expected declaration after 'pub'",
                    Current!.Position,
                    Current!.EndPosition
                ),
            };
        }

        return Current?.Kind switch
        {
            TokenKind.Module => ParseModule(),
            TokenKind.Struct => ParseStruct(),
            TokenKind.Fn => ParseFn(),
            TokenKind.Let => ParseLet(),
            TokenKind.Alias => ParseAlias(),
            TokenKind.Unalias => ParseUnalias(),
            _ => ParseExpr(),
        };
    }

    private Expr ParseModule(AccessLevel accessLevel = AccessLevel.Private)
    {
        EatExpected(TokenKind.Module);
        var identifier = EatExpected(TokenKind.Identifier);
        var moduleScope = new ModuleScope(
            accessLevel,
            identifier.Value,
            _scope,
            _scope.ModuleScope.FilePath,
            new Ast(Array.Empty<Expr>())
        );
        _scope.ModuleScope.AddModule(identifier.Value, moduleScope);
        var block = ParseBlock(StructureKind.Module, couldBeDictionary: false, moduleScope);
        moduleScope.Ast = new Ast(block.Expressions);

        return new ModuleExpr(accessLevel, identifier, block);
    }

    private Expr ParseStruct(AccessLevel accessLevel = AccessLevel.Private)
    {
        var startPos = EatExpected(TokenKind.Struct).Position;
        var identifier = EatExpected(TokenKind.Identifier);
        var parameters = ParseParameterList();
        var structExpr = new StructExpr(
            accessLevel,
            identifier,
            parameters,
            _scope.ModuleScope,
            startPos,
            Previous!.Position
        );
        _scope.ModuleScope.AddStruct(structExpr);

        return structExpr;
    }

    private Expr ParseFn(AccessLevel accessLevel = AccessLevel.Private)
    {
        EatExpected(TokenKind.Fn);
        var identifier = EatExpected(TokenKind.Identifier);
        var parameters = ParseParameterList();
        var functionScope = new LocalScope(_scope);
        foreach (var parameter in parameters)
        {
            functionScope.AddVariable(parameter.Identifier.Value, RuntimeNil.Value);
        }

        // Has closure?
        var hasClosure = false;
        if (AdvanceIf(TokenKind.EqualsGreater))
        {
            if (MatchIdentifier("closure"))
            {
                Eat();
                hasClosure = true;
            }
            else
            {
                throw Error("Expected 'closure'");
            }
        }

        var block = ParseBlockOrSingle(StructureKind.Function, functionScope);
        var function = new FunctionExpr(
            accessLevel,
            identifier,
            parameters,
            block,
            _scope.ModuleScope,
            hasClosure
        );

        _scope.ModuleScope.AddFunction(function);

        return function;
    }

    private LetExpr ParseLet()
    {
        var startPos = EatExpected(TokenKind.Let).Position;

        var identifierList = ParseIdentifierList();
        EatExpected(TokenKind.Equals);
        foreach (var identifier in identifierList)
            _scope.AddVariable(identifier.Value, RuntimeNil.Value);

        return new LetExpr(identifierList, ParseExpr(), _scope, startPos);
    }

    private Expr ParseAlias()
    {
        var pos = EatExpected(TokenKind.Alias).Position;
        var name = EatExpected(TokenKind.Identifier).Value;
        EatExpected(TokenKind.Equals);
        var value = EatExpected(TokenKind.DoubleQuoteStringLiteral, TokenKind.SingleQuoteStringLiteral);
        var arguments = value.Value
            .Split(' ')
            .Select(x =>
                new LiteralExpr(
                    new Token(TokenKind.TextArgumentStringLiteral, x, pos),
                    _scope
                )
            );

        _scope.ModuleScope.AddAlias(name, arguments);

        return new LiteralExpr(
            new Token(TokenKind.Nil, "nil", pos),
            _scope
        );
    }

    private LiteralExpr ParseUnalias()
    {
        var pos = EatExpected(TokenKind.Unalias).Position;
        var name = EatExpected(TokenKind.Identifier).Value;
        _scope.ModuleScope.RemoveAlias(name);

        return new LiteralExpr(
            new Token(TokenKind.Nil, "nil", pos),
            _scope
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
            var variadic = false;

            if (AdvanceIf(TokenKind.Equals))
                defaultValue = ParseExpr();

            if (AdvanceIf(TokenKind.DotDot))
            {
                EatExpected(TokenKind.Dot);
                variadic = true;
            }

            parameters.Add(new Parameter(identifier, defaultValue, variadic));
        } while (AdvanceIf(TokenKind.Comma));

        EatExpected(TokenKind.ClosedParenthesis);

        return parameters;
    }

    private Expr ParseExpr()
    {
        SkipWhiteSpace();

        while (AdvanceIf(TokenKind.Semicolon))
            SkipWhiteSpace();

        return ParseBinaryIf();
    }

    private Expr ParseBinaryIf(Expr? existingLeft = null)
    {
        var leftLine = Current?.Position.Line ?? 0;
        var left = existingLeft ?? ParseKeyword();
        if (existingLeft != null || leftLine == Current?.Position.Line && Match(TokenKind.If))
        {
            var op = Eat();
            _allowEndOfExpression = true;
            var right = ParseKeyword();

            return new BinaryExpr(left, op.Kind, right, _scope);
        }

        return left;
    }

    private Expr ParseKeyword()
    {
        if (Match(TokenKind.Return, TokenKind.Break, TokenKind.Continue, TokenKind.Throw))
        {
            var keyword = Eat();
            if (Current?.Kind == TokenKind.NewLine ||
                Current?.Kind == TokenKind.WhiteSpace && Peek()?.Kind == TokenKind.NewLine)
            {
                return new KeywordExpr(
                    keyword,
                    null,
                    _scope
                );
            }

            if (Match(TokenKind.If))
            {
                return ParseBinaryIf(
                    new KeywordExpr(keyword, null, _scope)
                );
            }

            _allowEndOfExpression = true;
            var value = ParseAssignment();

            return new KeywordExpr(
                keyword,
                value is EmptyExpr ? null : value,
                _scope
            );
        }

        return ParseAssignment();
    }

    private Expr ParseAssignment()
    {
        var left = ParseOr();
        if (Match(
                TokenKind.Equals,
                TokenKind.PlusEquals,
                TokenKind.MinusEquals,
                TokenKind.StarEquals,
                TokenKind.SlashEquals,
                TokenKind.QuestionQuestionEquals
            ))
        {
            var op = Eat();
            var right = ParseOr();

            // Eg. ENV_VAR=1: program-call
            if (MatchIgnoreWhiteSpace(TokenKind.Colon, TokenKind.Comma))
            {
                var identifierExpr = Eat().Kind == TokenKind.Comma
                    ? ParseAssignment()
                    : ParseIdentifier();
                if (identifierExpr is not CallExpr callExpr)
                    throw Error("Expected call to the right of colon after environment variable assignment");

                var variableName = left switch
                {
                    CallExpr expr => expr.Identifier.Value,
                    VariableExpr expr => expr.Identifier.Value,
                    _ => throw Error(
                        "Expected variable name to the left of equal sign in environment variable assignment"
                    ),
                };
                var variableValue = right switch
                {
                    CallExpr expr => new LiteralExpr(
                        expr.Identifier with { Kind = TokenKind.DoubleQuoteStringLiteral },
                        _scope
                    ),
                    VariableExpr expr => new LiteralExpr(
                        expr.Identifier with { Kind = TokenKind.DoubleQuoteStringLiteral },
                        _scope
                    ),
                    _ => right,
                };

                callExpr.EnvironmentVariables.Add(variableName, variableValue);

                return callExpr;
            }

            return op.Kind switch
            {
                TokenKind.Equals => new BinaryExpr(left, TokenKind.Equals, right, _scope),
                TokenKind.PlusEquals => new BinaryExpr(
                    left,
                    TokenKind.Equals,
                    new BinaryExpr(left, TokenKind.Plus, right, _scope),
                    _scope
                ),
                TokenKind.MinusEquals => new BinaryExpr(
                    left,
                    TokenKind.Equals,
                    new BinaryExpr(left, TokenKind.Minus, right, _scope),
                    _scope
                ),
                TokenKind.StarEquals => new BinaryExpr(
                    left,
                    TokenKind.Equals,
                    new BinaryExpr(left, TokenKind.Star, right, _scope),
                    _scope
                ),
                TokenKind.SlashEquals => new BinaryExpr(
                    left,
                    TokenKind.Equals,
                    new BinaryExpr(left, TokenKind.Slash, right, _scope),
                    _scope
                ),
                TokenKind.QuestionQuestionEquals => new IfExpr(
                    new UnaryExpr(TokenKind.Not, LValueToCondition(left), _scope),
                    new BlockExpr(
                        [new BinaryExpr(left, TokenKind.Equals, right, _scope)],
                        StructureKind.Other,
                        new LocalScope(_scope),
                        left.StartPosition,
                        right.EndPosition
                    ),
                    null,
                    _scope
                ),
                _ => throw new ArgumentOutOfRangeException(),
            };
        }

        return left;
    }

    private Expr LValueToCondition(Expr lvalue)
    {
        if (lvalue is IndexerExpr indexer)
        {
            return new BinaryExpr(
                indexer.Index,
                TokenKind.In,
                indexer.Value,
                _scope
            );
        }

        return new BinaryExpr(
            lvalue,
            TokenKind.NotEquals,
            new LiteralExpr(
                new Token(TokenKind.Nil, "nil", lvalue.StartPosition),
                _scope
            ),
            _scope
        );
    }

    private Expr ParseOr()
    {
        var left = ParseAnd();
        while (Match(TokenKind.Or, TokenKind.PipePipe))
        {
            var op = Eat().Kind;
            var right = ParseAnd();

            left = new BinaryExpr(left, op, right, _scope);
        }

        return left;
    }

    private Expr ParseAnd()
    {
        var left = ParsePipe();
        while (Match(TokenKind.And, TokenKind.AmpersandAmpersand))
        {
            var op = Eat().Kind;
            var right = ParsePipe();

            left = new BinaryExpr(left, op, right, _scope);
        }

        return left;
    }

    private Expr ParsePipe()
    {
        var left = ParseComparison();
        while (Match(TokenKind.Pipe, TokenKind.PipeErr, TokenKind.PipeAll))
        {
            var op = Eat().Kind;
            var right = ParseComparison();

            left = new BinaryExpr(left, op, right, _scope);
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
                   TokenKind.NotEquals,
                   TokenKind.In
               ))
        {
            var op = Eat().Kind;
            var right = ParseRange();

            left = new BinaryExpr(left, op, right, _scope);
        }

        return left;
    }

    private Expr ParseRange()
    {
        if (Peek()?.Kind is not TokenKind.Slash && AdvanceIf(TokenKind.DotDot))
        {
            var inclusive = AdvanceIf(TokenKind.Equals);

            return new RangeExpr(null, ParseCoalescing(), inclusive, _scope);
        }

        var left = ParseCoalescing();
        if (Peek()?.Kind is not TokenKind.Slash && AdvanceIf(TokenKind.DotDot))
        {
            var inclusive = AdvanceIf(TokenKind.Equals);

            _allowEndOfExpression = true;
            var right = ParseCoalescing();

            return right is EmptyExpr
                ? new RangeExpr(left, null, false, _scope)
                : new RangeExpr(left, right, inclusive, _scope);
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

            left = new BinaryExpr(left, op, right, _scope);
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

            left = new BinaryExpr(left, op, right, _scope);
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

            left = new BinaryExpr(left, op, right, _scope);
        }

        return left;
    }

    private Expr ParseExponent()
    {
        var left = ParseUnary();
        if (Match(TokenKind.Caret))
        {
            Eat();

            return new BinaryExpr(left, TokenKind.Caret, ParseExponent(), _scope);
        }

        return left;
    }

    private Expr ParseUnary()
    {
        if (Match(TokenKind.Minus, TokenKind.Not))
        {
            var op = Eat().Kind;
            var value = ParseIndexer();

            return new UnaryExpr(op, value, _scope);
        }

        return ParseIndexer();
    }

    private Expr ParseIndexer()
    {
        var expr = ParsePrimary();
        while (Match(TokenKind.OpenSquareBracket, TokenKind.Arrow))
        {
            if (AdvanceIf(TokenKind.Arrow))
            {
                var identifier = EatExpected(TokenKind.Identifier);

                expr = new FieldAccessExpr(expr, identifier, _scope);
                continue;
            }

            var precededByNewLineFollowedByWhiteSpace = Previous?.Kind == TokenKind.WhiteSpace &&
                _tokens.ElementAtOrDefault(_index - 2)?.Kind == TokenKind.NewLine;
            if (Previous?.Kind == TokenKind.NewLine || precededByNewLineFollowedByWhiteSpace)
                break;

            Eat(); // [
            var index = ParseExpr();
            EatExpected(TokenKind.ClosedSquareBracket);

            expr = new IndexerExpr(expr, index, _scope);
        }

        return expr;
    }

    private Expr ParsePrimary()
    {
        SkipWhiteSpace();
        if (Current != null && IsLiteral(Current.Kind))
        {
            return ParseLiteral();
        }

        if (Match(TokenKind.OpenParenthesis))
        {
            return ParseParenthesis();
        }

        if (Match(TokenKind.New))
        {
            return ParseNew();
        }

        if (Match(TokenKind.If))
        {
            return ParseIf();
        }

        if (Match(TokenKind.For))
        {
            return ParseFor();
        }

        if (Match(TokenKind.While))
        {
            return ParseWhile();
        }

        if (Match(TokenKind.Try))
        {
            return ParseTry();
        }

        if (Match(TokenKind.OpenSquareBracket))
        {
            return ParseList();
        }

        if (Match(TokenKind.OpenBrace))
        {
            return ParseBlock(StructureKind.Other, couldBeDictionary: true);
        }

        if (Match(TokenKind.Identifier, TokenKind.Ampersand, TokenKind.Dot, TokenKind.DotDot, TokenKind.Slash, TokenKind.Tilde))
        {
            return ParseIdentifier();
        }

        if (_allowEndOfExpression)
        {
            AdvanceIf(TokenKind.Unknown);

            return new EmptyExpr(_scope);
        }

        throw Current == null
            ? Error("Unexpected end of expression")
            : Error($"Unexpected token: '{Current?.Kind}'");
    }

    private Expr ParseClosure(Expr function)
    {
        EatExpected(TokenKind.EqualsGreater);
        if (function is not CallExpr left)
            throw Error("Expected function call or reference to the left of closure");

        if (Match(TokenKind.Ampersand))
        {
            var call = ParseIdentifier();
            if (call is not CallExpr { IsReference: true } functionReference)
                throw Error("Expected function or function reference to the right of closure.");

            var block = new BlockExpr(
                [functionReference],
                StructureKind.Function,
                new LocalScope(_scope),
                Previous!.Position,
                Previous!.EndPosition
            );

            return new ClosureExpr(left, [], block, _scope);
        }

        var scope = new LocalScope(_scope);
        var parameters = new List<Token>();
        if (Match(TokenKind.Identifier))
        {
            do
            {
                var parameter = EatExpected(TokenKind.Identifier);
                parameters.Add(parameter);
                scope.AddVariable(parameter.Value, RuntimeNil.Value);
            }
            while (AdvanceIf(TokenKind.Comma));
        }

        BlockExpr right;
        if (AdvanceIf(TokenKind.Colon))
        {
            _scope = scope;

            var bodyExpr = ParseComparison();
            right = new BlockExpr(
                [bodyExpr],
                StructureKind.Function,
                scope,
                bodyExpr.StartPosition,
                bodyExpr.EndPosition
            );

            _scope = _scope.Parent!;
        }
        else
        {
            right = ParseBlock(StructureKind.Function, couldBeDictionary: false, scope);
        }

        return new ClosureExpr(left, parameters, right, _scope);
    }


    private Expr ParseLiteral()
    {
        return Current!.Kind switch
        {
            TokenKind.IntegerLiteral or TokenKind.FloatLiteral => new LiteralExpr(Eat(), _scope),
            TokenKind.SingleQuoteStringLiteral => new LiteralExpr(Eat(), _scope),
            TokenKind.DoubleQuoteStringLiteral => ParseDoubleQuoteStringLiteral(),
            _ => new LiteralExpr(Eat(), _scope),
        };
    }

    private StringInterpolationExpr ParseDoubleQuoteStringLiteral()
    {
        var stringLiteral = EatExpected(TokenKind.DoubleQuoteStringLiteral);
        var parts = StringInterpolationParser.Parse(stringLiteral);
        var parsedParts = new List<Expr>();
        foreach (var part in parts)
        {
            var textPos = stringLiteral.Position with
            {
                Index = stringLiteral.Position.Index + part.Offset,
                Column = stringLiteral.Position.Column + part.Offset,
                FilePath = _scope.ModuleScope.FilePath
            };

            if (part.Kind == InterpolationPartKind.Text)
            {
                var token = new Token(
                    TokenKind.DoubleQuoteStringLiteral,
                    part.Value,
                    textPos
                );
                parsedParts.Add(new LiteralExpr(token, _scope));
            }
            else
            {
                var startPos = textPos with
                {
                    Column = textPos.Column + 3, // Start after: "${
                };
                var tokens = Lexer.Lex(part.Value, startPos, out var lexError);
                if (lexError != null)
                    throw lexError;

                var ast = Parse(tokens, _scope);
                if (ast.Expressions.Count != 1)
                    throw new RuntimeException(
                        "Expected exactly one expression in the string interpolation block",
                        textPos,
                        tokens.LastOrDefault()?.EndPosition ?? textPos
                    );

                foreach (var expr in ast.Expressions)
                    expr.IsRoot = false;

                parsedParts.Add(ast.Expressions.First());
            }
        }

        return new StringInterpolationExpr(parsedParts, stringLiteral.Position, _scope);
    }

    private Expr ParseParenthesis()
    {
        var startPos = EatExpected(TokenKind.OpenParenthesis).Position;
        var expressions = new List<Expr>();
        do
        {
            expressions.Add(ParseExpr());
        } while (AdvanceIf(TokenKind.Comma) && !Match(TokenKind.ClosedParenthesis));

        var endPos = EatExpected(TokenKind.ClosedParenthesis).Position;

        return expressions.Count == 1
            ? expressions.First()
            : new TupleExpr(expressions, _scope, startPos, endPos);
    }

    private NewExpr ParseNew()
    {
        var startPos = EatExpected(TokenKind.New).Position;

        var modulePath = new List<Token>();
        do
        {
            modulePath.Add(EatExpected(TokenKind.Identifier));
        } while (AdvanceIf(TokenKind.ColonColon));

        var identifier = modulePath.Last();
        modulePath.RemoveAt(modulePath.Count - 1);

        var arguments = new List<Expr>();
        if (AdvanceIf(TokenKind.OpenParenthesis))
        {
            do
            {
                if (!Match(TokenKind.ClosedParenthesis))
                    arguments.Add(ParseExpr());
            } while (AdvanceIf(TokenKind.Comma));

            EatExpected(TokenKind.ClosedParenthesis);
        }
        else
        {
            arguments = ParseTextArguments();
        }

        return new NewExpr(
            identifier,
            modulePath,
            arguments,
            _scope,
            startPos,
            Previous!.EndPosition
        );
    }

    private IfExpr ParseIf()
    {
        EatExpected(TokenKind.If);

        var condition = ParseExpr();
        var thenBranch = ParseBlockOrSingle(StructureKind.Other);
        var elseBranch = AdvanceIf(TokenKind.Else)
            ? ParseExpr()
            : null;

        return new IfExpr(condition, thenBranch, elseBranch, _scope);
    }

    private ForExpr ParseFor()
    {
        EatExpected(TokenKind.For);
        var identifierList = ParseIdentifierList();
        EatExpected(TokenKind.In);
        var value = ParseExpr();

        var scope = new LocalScope(_scope);
        foreach (var identifier in identifierList)
            scope.AddVariable(identifier.Value, RuntimeNil.Value);

        var branch = ParseBlockOrSingle(StructureKind.Loop, scope);

        return new ForExpr(identifierList, value, branch, _scope);
    }

    private WhileExpr ParseWhile()
    {
        EatExpected(TokenKind.While);
        var condition = ParseExpr();
        var branch = ParseBlockOrSingle(StructureKind.Loop);

        return new WhileExpr(condition, branch, _scope);
    }

    private TryExpr ParseTry()
    {
        EatExpected(TokenKind.Try);
        var body = ParseBlockOrSingle(StructureKind.Other);
        var catchExpressions = new List<CatchExpr>();
        while (AdvanceIf(TokenKind.Catch))
        {
            Token? catchIdentifier = null;
            TypeExpr? catchType = null;
            if (AdvanceIf(TokenKind.Identifier))
            {
                catchIdentifier = Previous;

                if (AdvanceIf(TokenKind.With))
                {
                    var type = ParseIdentifier();
                    if (type is not TypeExpr typeExpr)
                        throw Error("Expected type after catch identifier");

                    catchType = typeExpr;
                }
            }

            var catchScope = new LocalScope(_scope);
            if (catchIdentifier != null)
                catchScope.AddVariable(catchIdentifier.Value, RuntimeNil.Value);

            var catchBody = ParseBlockOrSingle(StructureKind.Other, catchScope);
            catchExpressions.Add(
                new CatchExpr(catchIdentifier, catchType, catchBody, _scope)
            );
        }

        if (catchExpressions.Count == 0)
            throw Error("Expected catch block after try");

        return new TryExpr(body, catchExpressions, _scope);
    }

    private List<Token> ParseIdentifierList()
    {
        if (Match(TokenKind.Identifier))
            return [Eat()];

        EatExpected(TokenKind.OpenParenthesis);

        var identifiers = new List<Token>();
        do
        {
            identifiers.Add(EatExpected(TokenKind.Identifier));
        } while (AdvanceIf(TokenKind.Comma));

        EatExpected(TokenKind.ClosedParenthesis);

        return identifiers;
    }

    private ListExpr ParseList()
    {
        var startPos = EatExpected(TokenKind.OpenSquareBracket).Position;

        var expressions = new List<Expr>();
        do
        {
            if (Match(TokenKind.ClosedSquareBracket))
                break;

            expressions.Add(ParseExpr());
        } while (AdvanceIf(TokenKind.Comma));

        var endPos = EatExpected(TokenKind.ClosedSquareBracket).Position;

        return new ListExpr(expressions, _scope, startPos, endPos);
    }

    private BlockExpr ParseBlockOrSingle(StructureKind parentStructureKind, LocalScope? scope = null, bool couldBeDictionary = false)
    {
        if (AdvanceIf(TokenKind.Colon))
        {
            var blockScope = scope ?? new LocalScope(_scope);
            _scope = blockScope;
            var expr = ParseExpr();
            _scope = _scope.Parent!;

            return new BlockExpr(
                [expr],
                parentStructureKind,
                blockScope,
                expr.StartPosition,
                expr.EndPosition
            );
        }

        return ParseBlock(parentStructureKind, couldBeDictionary, scope);
    }

    private Expr ParseBlock(StructureKind parentStructureKind, bool couldBeDictionary = false, bool orAsOtherStructure = true)
        => ParseBlock(parentStructureKind, orAsOtherStructure, couldBeDictionary, null);

    private BlockExpr ParseBlock(StructureKind parentStructureKind, bool couldBeDictionary, Scope? scope)
        => (BlockExpr)ParseBlock(parentStructureKind, couldBeDictionary, orAsOtherStructure: false, scope);

    private Expr ParseBlock(
        StructureKind parentStructureKind,
        bool couldBeDictionary,
        bool orAsOtherStructure,
        Scope? scope)
    {
        var startPos = EatExpected(TokenKind.OpenBrace).Position;
        var blockScope = scope ?? new LocalScope(_scope);
        _scope = blockScope;

        var expressions = new List<Expr>();
        while (!AdvanceIf(TokenKind.ClosedBrace))
        {
            expressions.Add(ParseExprOrDecl());
            if (!orAsOtherStructure)
                continue;

            if (Match(TokenKind.Comma) && expressions.Count == 1)
                return ContinueParseAsSet(expressions.First());
            if (Match(TokenKind.Colon) && expressions.Count == 1)
                return ContinueParseAsDictionary(expressions.First());
        }

        _scope = _scope.Parent!;

        if (expressions.Count == 0 && couldBeDictionary)
            return new DictionaryExpr([], _scope, startPos, Previous!.EndPosition);

        return new BlockExpr(
            expressions,
            parentStructureKind,
            blockScope,
            startPos,
            Previous!.EndPosition
        );
    }

    private SetExpr ContinueParseAsSet(Expr firstExpression)
    {
        _scope = _scope.Parent!;

        var expressions = new List<Expr>
        {
            firstExpression,
        };

        while (AdvanceIf(TokenKind.Comma) && !Match(TokenKind.ClosedBrace))
            expressions.Add(ParseExpr());

        var endPosition = EatExpected(TokenKind.ClosedBrace).Position;

        return new SetExpr(
            expressions,
            _scope,
            firstExpression.StartPosition,
            endPosition
        );
    }

    private Expr ContinueParseAsDictionary(Expr firstExpression)
    {
        _scope = _scope.Parent!;
        if (firstExpression is not (LiteralExpr or StringInterpolationExpr or VariableExpr))
            throw Error("Expected literal or variable as dictionary key");

        EatExpected(TokenKind.Colon);
        var expressions = new List<(Expr, Expr)>
        {
            (firstExpression, ParseExpr()),
        };

        while (AdvanceIf(TokenKind.Comma) && !Match(TokenKind.ClosedBrace))
        {
            var key = ParseLiteral();
            EatExpected(TokenKind.Colon);
            expressions.Add((key, ParseExpr()));
        }

        var endPos = EatExpected(TokenKind.ClosedBrace).Position;

        return new DictionaryExpr(
            expressions,
            _scope,
            firstExpression.StartPosition,
            endPos
        );
    }

    private Expr ParseIdentifier()
    {
        var isReference = AdvanceIf(TokenKind.Ampersand);
        var pos = Current?.Position ?? TextPos.Default;
        IList<Token> modulePath = Array.Empty<Token>();
        while (Match(TokenKind.Identifier) && Peek()?.Kind == TokenKind.ColonColon)
        {
            if (modulePath.IsReadOnly)
                modulePath = new List<Token>();

            modulePath.Add(Eat());
            Eat(); // ::
        }

        var identifier = Match(TokenKind.Identifier)
            ? Eat()
            : new Token(TokenKind.Identifier, ParsePath(), pos);

        if (StdBindings.HasRuntimeType(identifier.Value) || _scope.ModuleScope.ContainsStruct(identifier.Value))
            return new TypeExpr(identifier, _scope);

        var importedStdModule = _scope.ModuleScope.FindImportedStdFunctionModule(identifier.Value)
            ?? _scope.ModuleScope.FindImportedStdStructModule(identifier.Value);
        if (modulePath.Count == 0 && importedStdModule != null)
        {
            modulePath = new List<Token>
            {
                new(TokenKind.Identifier, importedStdModule, TextPos.Default),
            };
        }

        if (Current?.Kind == TokenKind.OpenParenthesis)
        {
            Eat();
            var arguments = new List<Expr>();

            // Load alias if there is one
            var alias = UseAliases ? _scope.ModuleScope.FindAlias(identifier.Value) : null;
            if (alias != null)
            {
                identifier = identifier with { Value = alias.Name };
                arguments.AddRange(alias.Arguments);
            }

            do
            {
                if (!Match(TokenKind.ClosedParenthesis))
                    arguments.Add(ParseExpr());
            }
            while (AdvanceIf(TokenKind.Comma));

            var endPos = EatExpected(TokenKind.ClosedParenthesis).EndPosition;
            var functionPlurality = ParsePlurality(identifier, out var modifiedIdentifier);

            var call = new CallExpr(
                modifiedIdentifier,
                modulePath,
                arguments,
                CallStyle.Parenthesized,
                functionPlurality,
                CallType.Unknown,
                _scope,
                endPos
            )
            {
                IsReference = isReference,
            };

            return MatchIgnoreWhiteSpace(TokenKind.EqualsGreater)
                ? ParseClosure(call)
                : call;
        }

        if (modulePath.Count == 0 &&
            (identifier.Value.StartsWith('$') || _scope.HasVariable(identifier.Value)))
        {
            return new VariableExpr(identifier, _scope);
        }

        var textArguments = ParseTextArguments();

        // Load alias if there is one
        var aliasResult = UseAliases ? _scope.ModuleScope.FindAlias(identifier.Value) : null;
        if (aliasResult != null)
        {
            identifier = identifier with { Value = aliasResult.Name };
            textArguments.InsertRange(0, aliasResult.Arguments);
        }

        var plurality = ParsePlurality(identifier, out var newIdentifier);

        var textCall = new CallExpr(
            newIdentifier,
            modulePath,
            textArguments,
            CallStyle.TextArguments,
            plurality,
            CallType.Unknown,
            _scope,
            textArguments.LastOrDefault()?.EndPosition ?? identifier.EndPosition
        )
        {
            IsReference = isReference,
        };

        return Match(TokenKind.EqualsGreater)
            ? ParseClosure(textCall)
            : textCall;
    }

    private Plurality ParsePlurality(Token identifier, out Token newToken)
    {
        if (identifier.Value.EndsWith('!'))
        {
            newToken = identifier with { Value = identifier.Value[..^1] };

            return Plurality.Plural;
        }

        newToken = identifier;

        return Plurality.Singular;
    }

    private List<Expr> ParseTextArguments()
    {
        if (!MatchInclWhiteSpace(TokenKind.WhiteSpace))
            return [];

        var textArguments = new List<Expr>();
        var interpolationParts = new List<Expr>();
        var currentText = new StringBuilder();
        AdvanceIf(TokenKind.WhiteSpace);
        AdvanceIf(TokenKind.Backslash);
        var pos = Current?.Position ?? TextPos.Default;

        while (!ReachedTextEnd())
        {
            AdvanceIf(TokenKind.Backslash);

            if (Previous?.Kind != TokenKind.Backslash && AdvanceIf(TokenKind.WhiteSpace))
            {
                var token = new Token(
                    TokenKind.TextArgumentStringLiteral,
                    currentText.ToString(),
                    pos
                );
                if (currentText.Length > 0)
                {
                    interpolationParts.Add(new LiteralExpr(token, _scope));
                    currentText.Clear();
                }

                textArguments.Add(new StringInterpolationExpr(interpolationParts, pos, _scope));
                interpolationParts = [];
                pos = Current?.Position ?? TextPos.Default;
                continue;
            }

            var next = Peek();
            var isSingleQuoteStringLiteral = MatchInclWhiteSpace(TokenKind.SingleQuoteStringLiteral);
            var isDoubleQuoteStringLiteral = MatchInclWhiteSpace(TokenKind.DoubleQuoteStringLiteral);
            var isDollar = MatchInclWhiteSpace(TokenKind.Identifier) &&
                Current!.Value.StartsWith('$') &&
                Previous?.Value != "\\";
            if (MatchInclWhiteSpace(TokenKind.Tilde) &&
                (next == null || next.Kind is TokenKind.Slash or TokenKind.WhiteSpace))
            {
                Eat();
                currentText.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
            else if (isSingleQuoteStringLiteral || isDoubleQuoteStringLiteral || isDollar)
            {
                var stringToken = new Token(
                    isSingleQuoteStringLiteral
                        ? TokenKind.SingleQuoteStringLiteral
                        : TokenKind.DoubleQuoteStringLiteral,
                    currentText.ToString(),
                    pos
                );
                interpolationParts.Add(new LiteralExpr(stringToken, _scope));
                currentText.Clear();

                if (isSingleQuoteStringLiteral)
                {
                    interpolationParts.Add(new LiteralExpr(Eat(), _scope));
                }
                else if (isDoubleQuoteStringLiteral)
                {
                    interpolationParts.Add(ParseDoubleQuoteStringLiteral());
                }
                else if (isDollar && next?.Kind == TokenKind.OpenBrace)
                {
                    Eat();
                    interpolationParts.Add(ParseBlock(StructureKind.Other));
                }
                else if (isDollar)
                {
                    var identifier = Eat();
                    // Environment variable
                    if (identifier.Value.Length > 1)
                        interpolationParts.Add(new VariableExpr(identifier, _scope));
                }
            }
            else
            {
                currentText.Append(Eat().Value);
            }
        }

        // There might still be some text left that needs to be added since
        // currentText is only moved to textArguments when encountering a space,
        // which normally are not present at the end.
        if (currentText.Length > 0)
        {
            var finalToken = new Token(
                TokenKind.TextArgumentStringLiteral,
                currentText.ToString(),
                pos
            );
            interpolationParts.Add(new LiteralExpr(finalToken, _scope));
        }

        if (interpolationParts.Any())
        {
            textArguments.Add(new StringInterpolationExpr(interpolationParts, pos, _scope));
        }

        return textArguments;
    }

    private string ParsePath()
    {
        var value = new StringBuilder();
        while (!ReachedTextEnd() &&
               !MatchInclWhiteSpace(TokenKind.WhiteSpace, TokenKind.OpenParenthesis, TokenKind.OpenSquareBracket))
        {
            AdvanceIf(TokenKind.Backslash);

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
        var reachedComment = Previous?.Kind == TokenKind.WhiteSpace &&
            MatchInclWhiteSpace(TokenKind.Comment);

        return ReachedEnd || reachedComment || MatchInclWhiteSpace(
            TokenKind.AmpersandAmpersand,
            TokenKind.PipePipe,
            TokenKind.EqualsGreater,
            TokenKind.ClosedParenthesis,
            TokenKind.OpenBrace,
            TokenKind.ClosedBrace,
            TokenKind.Pipe,
            TokenKind.PipeErr,
            TokenKind.PipeAll,
            TokenKind.Semicolon,
            TokenKind.NewLine
        ) && Previous?.Kind != TokenKind.Backslash;
    }

    private bool AdvanceIf(TokenKind kind)
    {
        var prevIndex = _index;

        if (Match(kind))
        {
            Eat();
            return true;
        }

        _index = prevIndex;

        return false;
    }

    private Token EatExpected(params TokenKind[] kinds)
    {
        if (Match(kinds))
        {
            return Eat();
        }

        throw Error($"Expected '{kinds.First()}' but got '{Current?.Kind}'");
    }

    private Token Eat()
    {
        if (_allowEndOfExpression && Current?.Kind == TokenKind.NewLine)
        {
            _tokens[_index] = new Token(TokenKind.Unknown, "", TextPos.Default);

            return _tokens[_index];
        }

        _allowEndOfExpression = false;
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

    private bool MatchIgnoreWhiteSpace(params TokenKind[] kinds)
    {
        var index = _index;
        while (IsWhiteSpace(_tokens.ElementAtOrDefault(index)?.Kind))
            index++;

        return kinds.Any(x => x == _tokens.ElementAtOrDefault(index)?.Kind);
    }

    private bool MatchIdentifier(string value)
        => Match(TokenKind.Identifier) && Current?.Value == value;

    private bool IsLiteral(TokenKind kind)
        => kind is
            TokenKind.IntegerLiteral or
            TokenKind.FloatLiteral or
            TokenKind.SingleQuoteStringLiteral or
            TokenKind.DoubleQuoteStringLiteral or
            TokenKind.BashLiteral or
            TokenKind.Nil or
            TokenKind.True or
            TokenKind.False;

    private Token? Peek(int length = 1)
        => _tokens.Count > _index + length
            ? _tokens[_index + length]
            : null;

    private bool MatchInclWhiteSpace(params TokenKind[] kinds)
        => Current != null && kinds.Contains(Current.Kind);

    private RuntimeException Error(string message)
    {
        var token = Current == null && _index > 0
            ? _tokens[_index - 1]
            : Current;

        return new RuntimeException(
            message,
            token?.Position ?? TextPos.Default,
            token?.EndPosition ?? TextPos.Default
        );
    }

    private void SkipWhiteSpace()
    {
        while (IsWhiteSpace(Current?.Kind))
            Eat();
    }

    private bool IsWhiteSpace(TokenKind? kind)
        => kind is TokenKind.WhiteSpace or TokenKind.NewLine or TokenKind.Comment;
}