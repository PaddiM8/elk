#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Elk.Interpreting;
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

    public static List<Expr> Parse(
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

            var expr = parser.ParseExpr();
            expr.IsRoot = true;
            expressions.Add(expr);
            parser.SkipWhiteSpace();
        }

        return expressions;
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
            } while (AdvanceIf(TokenKind.Comma));

            if (!MatchIdentifier("from"))
                throw new ParseException(Current?.Position ?? TextPos.Default, "Expected 'from'");

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
                    throw new ParseException(pos, $"Module does not contain function '{symbolImportToken.Value}'");

                _scope.ModuleScope.ImportStdFunction(symbolImportToken.Value, moduleName);
            }

            return;
        }

        var importScope = ImportUserModule(relativePath, moduleName, pos);
        foreach (var symbolImportToken in symbolImportTokens)
        {
            var importedFunction = importScope.FindFunction(symbolImportToken.Value, lookInImports: false);
            if (importedFunction != null)
            {
                _scope.ModuleScope.ImportFunction(importedFunction);
                continue;
            }

            var importedStruct = importScope.FindStruct(symbolImportToken.Value, lookInImports: false);
            if (importedStruct != null)
            {
                _scope.ModuleScope.ImportStruct(importedStruct);
                continue;
            }

            var importedModule = importScope.FindModule(new[] { symbolImportToken }, lookInImports: false);
            if (importedModule != null)
            {
                _scope.ModuleScope.ImportModule(symbolImportToken.Value, importedModule);
                continue;
            }

            if (importedModule == null)
            {
                throw new ParseException(
                    symbolImportToken.Position,
                    $"Module does not contain symbol '{symbolImportToken.Value}'"
                );
            }
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
        foreach (var symbol in importScope.Functions)
            _scope.ModuleScope.ImportFunction(symbol);

        foreach (var symbol in importScope.Structs)
            _scope.ModuleScope.ImportStruct(symbol);

        foreach (var module in importScope.Modules.Where(x => x.Name != null))
            _scope.ModuleScope.ImportModule(module.Name!, module);
    }

    private ModuleScope ImportUserModule(string path, string moduleName, TextPos pos)
    {
        var directoryPath = Path.GetDirectoryName(
            _scope.ModuleScope.FilePath ?? ShellEnvironment.WorkingDirectory
        )!;
        var absolutePath = Path.GetFullPath(Path.Combine(directoryPath, path) + ".elk");
        if (!File.Exists(absolutePath))
        {
            throw new ParseException(pos, $"Cannot find file '{absolutePath}'");
        }

        var importScope = _scope.ModuleScope.RootModule.FindRegisteredModule(absolutePath);
        if (importScope == null)
        {
            importScope = ModuleScope.CreateAsImported(
                moduleName,
                _scope.ModuleScope.RootModule,
                absolutePath
            );

            importScope.Ast = Parse(
                Lexer.Lex(File.ReadAllText(absolutePath), absolutePath, out var lexError),
                importScope
            );
            if (lexError != null)
                throw new ParseException(lexError.Position, lexError.Message);

            _scope.ModuleScope.RootModule.RegisterModule(absolutePath, importScope);
        }

        _scope.ModuleScope.ImportModule(moduleName, importScope);

        return importScope;
    }

    private Expr ParseExpr()
    {
        SkipWhiteSpace();

        while (AdvanceIf(TokenKind.Semicolon))
            SkipWhiteSpace();

        return Current?.Kind switch
        {
            TokenKind.Module => ParseModule(),
            TokenKind.Struct => ParseStruct(),
            TokenKind.Fn => ParseFn(),
            TokenKind.Alias => ParseAlias(),
            TokenKind.Unalias => ParseUnalias(),
            _ => ParseBinaryIf(),
        };
    }

    private Expr ParseModule()
    {
        EatExpected(TokenKind.Module);
        var identifier = EatExpected(TokenKind.Identifier);
        var moduleScope = new ModuleScope(identifier.Value, _scope, _scope.ModuleScope.FilePath);
        _scope.ModuleScope.AddModule(identifier.Value, moduleScope);
        var block = ParseBlock(StructureKind.Module, moduleScope);

        return new ModuleExpr(identifier, block);
    }

    private Expr ParseStruct()
    {
        EatExpected(TokenKind.Struct);
        var identifier = EatExpected(TokenKind.Identifier);
        var parameters = ParseParameterList();

        var structExpr = new StructExpr(identifier, parameters, _scope.ModuleScope);
        _scope.ModuleScope.AddStruct(structExpr);

        return structExpr;
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
                throw new ParseException(Current?.Position ?? TextPos.Default, "Expected 'closure'");
            }
        }

        var block = ParseBlockOrSingle(StructureKind.Function, functionScope);
        var function = new FunctionExpr(
            identifier,
            parameters,
            block,
            _scope.ModuleScope,
            hasClosure
        );

        _scope.ModuleScope.AddFunction(function);

        return function;
    }

    private Expr ParseAlias()
    {
        var pos = EatExpected(TokenKind.Alias).Position;
        var name = EatExpected(TokenKind.Identifier).Value;
        EatExpected(TokenKind.Equals);
        var value = EatExpected(TokenKind.StringLiteral);

        _scope.ModuleScope.AddAlias(name, new LiteralExpr(value));

        return new LiteralExpr(new Token(TokenKind.Nil, "nil", pos));
    }

    private Expr ParseUnalias()
    {
        var pos = EatExpected(TokenKind.Unalias).Position;
        var name = EatExpected(TokenKind.Identifier).Value;
        _scope.ModuleScope.RemoveAlias(name);

        return new LiteralExpr(new Token(TokenKind.Nil, "nil", pos));
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

    private Expr ParseBinaryIf(Expr? existingLeft = null)
    {
        var leftLine = Current?.Position.Line ?? 0;
        var left = existingLeft ?? ParseKeyword();
        if (existingLeft != null || leftLine == Current?.Position.Line && Match(TokenKind.If))
        {
            var op = Eat();
            _allowEndOfExpression = true;
            var right = ParseKeyword();

            return new BinaryExpr(left, op.Kind, right);
        }

        return left;
    }

    private Expr ParseKeyword()
    {
        if (Match(TokenKind.Return, TokenKind.Break, TokenKind.Continue))
        {
            var keyword = Eat();
            if (Match(TokenKind.If))
            {
                return ParseBinaryIf(
                    new KeywordExpr(keyword.Kind, null, keyword.Position)
                );
            }

            _allowEndOfExpression = true;
            var value = ParseAssignment();

            return new KeywordExpr(
                keyword.Kind,
                value is EmptyExpr ? null : value,
                keyword.Position
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
                TokenKind.SlashEquals
            ))
        {
            var op = Eat();
            var right = ParseOr();

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

    private Expr ParseOr()
    {
        var left = ParseAnd();
        while (Match(TokenKind.Or, TokenKind.PipePipe))
        {
            var op = Eat().Kind;
            var right = ParseAnd();

            left = new BinaryExpr(left, op, right);
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

            left = new BinaryExpr(left, op, right);
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
                   TokenKind.NotEquals,
                   TokenKind.In
               ))
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
            var inclusive = AdvanceIf(TokenKind.Equals);

            return new RangeExpr(null, ParseCoalescing(), inclusive);
        }

        var left = ParseCoalescing();
        if (Peek()?.Kind is not TokenKind.Slash && AdvanceIf(TokenKind.DotDot))
        {
            var inclusive = AdvanceIf(TokenKind.Equals);

            _allowEndOfExpression = true;
            var right = ParseCoalescing();

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
        if (Match(TokenKind.Minus, TokenKind.Not))
        {
            var op = Eat().Kind;
            var value = ParseIndexer();

            return new UnaryExpr(op, value);
        }

        return ParseIndexer();
    }

    private Expr ParseIndexer()
    {
        var expr = ParseFieldAccess();
        while (AdvanceIf(TokenKind.OpenSquareBracket))
        {
            var index = ParseExpr();
            EatExpected(TokenKind.ClosedSquareBracket);

            expr = new IndexerExpr(expr, index);
        }

        return expr;
    }

    private Expr ParseFieldAccess()
    {
        var left = ParsePrimary();
        while (AdvanceIf(TokenKind.Arrow))
        {
            var identifier = EatExpected(TokenKind.Identifier);

            left = new FieldAccessExpr(left, identifier);
        }

        return left;
    }

    private Expr ParsePrimary()
    {
        if (Current != null && IsLiteral(Current.Kind))
        {
            return ParseLiteral();
        }

        if (Match(TokenKind.OpenParenthesis))
        {
            return ParseParenthesis();
        }

        if (Match(TokenKind.Let))
        {
            return ParseLet();
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

        if (Match(TokenKind.Throw))
        {
            return ParseThrow();
        }

        if (Match(TokenKind.OpenSquareBracket))
        {
            return ParseList();
        }

        if (Match(TokenKind.OpenBrace))
        {
            return ParseBlock(StructureKind.Other);
        }

        if (Match(TokenKind.Identifier, TokenKind.Ampersand, TokenKind.Dot, TokenKind.DotDot, TokenKind.Slash, TokenKind.Tilde))
        {
            return ParseIdentifier();
        }

        if (_allowEndOfExpression)
        {
            AdvanceIf(TokenKind.Unknown);

            return new EmptyExpr();
        }

        throw Current == null
            ? Error("Unexpected end of expression")
            : Error($"Unexpected token: '{Current?.Kind}'");
    }

    private Expr ParseClosure(Expr function)
    {
        EatExpected(TokenKind.EqualsGreater);
        if (function is not CallExpr left)
        {
            throw new ParseException(
                Current?.Position ?? TextPos.Default, "Expected function call or reference to the left of closure"
            );
        }

        if (Match(TokenKind.Ampersand))
        {
            var call = ParseIdentifier();
            if (call is not CallExpr { IsReference: true } functionReference)
            {
                throw new ParseException(
                    Current?.Position ?? TextPos.Default,
                    "Expected function or function reference to the right of closure."
                );
            }

            var block = new BlockExpr(
                new List<Expr> { functionReference },
                StructureKind.Other,
                Previous!.Position,
                new LocalScope(_scope)
            );

            return new ClosureExpr(left, new List<Token>(), block);
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
                new List<Expr> { bodyExpr },
                StructureKind.Other,
                Previous!.Position,
                scope
            );

            _scope = _scope.Parent!;
        }
        else
        {
            right = ParseBlock(StructureKind.Other, scope);
        }

        return new ClosureExpr(left, parameters, right);
    }


    private Expr ParseLiteral()
    {
        return Current!.Kind switch
        {
            TokenKind.IntegerLiteral or TokenKind.FloatLiteral => new LiteralExpr(Eat()),
            TokenKind.StringLiteral => ParseStringLiteral(),
            _ => new LiteralExpr(Eat()),
        };
    }

    private Expr ParseStringLiteral()
    {
        var stringLiteral = EatExpected(TokenKind.StringLiteral);
        var parts = StringInterpolationParser.Parse(stringLiteral);
        var parsedParts = new List<Expr>();
        var column = stringLiteral.Position.Column;
        foreach (var part in parts)
        {
            var textPos = stringLiteral.Position with { Column = column, FilePath = _scope.ModuleScope.FilePath };
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
                var tokens = Lexer.Lex(part.Value, textPos, out var lexError);
                if (lexError != null)
                    throw new ParseException(lexError.Position, lexError.Message);

                var ast = Parse(tokens, _scope);
                if (ast.Count != 1)
                    throw new ParseException(
                        textPos, "Expected exactly one expression in the string interpolation block"
                    );

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
        } while (AdvanceIf(TokenKind.Comma) && !Match(TokenKind.ClosedParenthesis));

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

    private Expr ParseNew()
    {
        EatExpected(TokenKind.New);

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

        return new NewExpr(identifier, modulePath, arguments);
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

    private Expr ParseWhile()
    {
        EatExpected(TokenKind.While);
        var condition = ParseExpr();
        var branch = ParseBlockOrSingle(StructureKind.Loop);

        return new WhileExpr(condition, branch);
    }

    private Expr ParseTry()
    {
        EatExpected(TokenKind.Try);
        var body = ParseBlockOrSingle(StructureKind.Other);
        EatExpected(TokenKind.Catch);
        var catchIdentifier = AdvanceIf(TokenKind.Identifier)
            ? Previous
            : null;
        var catchScope = new LocalScope(_scope);
        if (catchIdentifier != null)
            catchScope.AddVariable(catchIdentifier.Value, RuntimeNil.Value);

        var catchBody = ParseBlockOrSingle(StructureKind.Other, catchScope);

        return new TryExpr(body, catchBody, catchIdentifier);
    }

    private Expr ParseThrow()
    {
        EatExpected(TokenKind.Throw);

        return new ThrowExpr(ParseExpr());
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
        } while (AdvanceIf(TokenKind.Comma));

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
        } while (AdvanceIf(TokenKind.Comma));

        EatExpected(TokenKind.ClosedSquareBracket);

        return new ListExpr(expressions, pos);
    }

    private BlockExpr ParseBlockOrSingle(StructureKind parentStructureKind, LocalScope? scope = null)
    {
        if (AdvanceIf(TokenKind.Colon))
        {
            var blockScope = scope ?? new LocalScope(_scope);
            _scope = blockScope;
            var expr = ParseExpr();
            _scope = _scope.Parent!;

            return new BlockExpr(
                new() { expr },
                parentStructureKind,
                expr.Position,
                blockScope
            );
        }

        return ParseBlock(parentStructureKind, scope);
    }

    private Expr ParseBlock(StructureKind parentStructureKind, bool orAsOtherStructure = true)
        => ParseBlock(parentStructureKind, orAsOtherStructure, null);

    private BlockExpr ParseBlock(StructureKind parentStructureKind, Scope? scope)
        => (BlockExpr)ParseBlock(parentStructureKind, orAsOtherStructure: false, scope);

    private Expr ParseBlock(
        StructureKind parentStructureKind,
        bool orAsOtherStructure,
        Scope? scope)
    {
        EatExpected(TokenKind.OpenBrace);

        var pos = Current!.Position;
        var blockScope = scope ?? new LocalScope(_scope);
        _scope = blockScope;

        var expressions = new List<Expr>();
        while (!AdvanceIf(TokenKind.ClosedBrace))
        {
            expressions.Add(ParseExpr());

            if (orAsOtherStructure && Match(TokenKind.Comma) && expressions.Count == 1)
                return ContinueParseAsSet(expressions.First());
            if (orAsOtherStructure && Match(TokenKind.Colon) && expressions.Count == 1)
                return ContinueParseAsDictionary(expressions.First());
        }

        if (expressions.Count == 0)
            return new DictionaryExpr(new List<(Expr, Expr)>(), pos);

        _scope = _scope.Parent!;

        return new BlockExpr(expressions, parentStructureKind, pos, blockScope);
    }

    private Expr ContinueParseAsSet(Expr firstExpression)
    {
        _scope = _scope.Parent!;

        var expressions = new List<Expr>
        {
            firstExpression,
        };

        while (AdvanceIf(TokenKind.Comma) && !Match(TokenKind.ClosedBrace))
            expressions.Add(ParseExpr());

        EatExpected(TokenKind.ClosedBrace);

        return new SetExpr(expressions, firstExpression.Position);
    }

    private Expr ContinueParseAsDictionary(Expr firstExpression)
    {
        _scope = _scope.Parent!;

        if (Previous == null || !(IsLiteral(Previous.Kind) || Previous.Kind == TokenKind.Identifier))
            throw Error("Expected literal or identifier as dictionary key");

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

        EatExpected(TokenKind.ClosedBrace);

        return new DictionaryExpr(expressions, firstExpression.Position);
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
            return new TypeExpr(identifier);

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
                if (alias.Value != null)
                    arguments.Add(alias.Value);
            }

            do
            {
                if (!Match(TokenKind.ClosedParenthesis))
                    arguments.Add(ParseExpr());
            } while (AdvanceIf(TokenKind.Comma));

            EatExpected(TokenKind.ClosedParenthesis);
            var functionPlurality = ParsePlurality(identifier, out var modifiedIdentifier);

            var call = new CallExpr(
                modifiedIdentifier,
                modulePath,
                arguments,
                CallStyle.Parenthesized,
                functionPlurality,
                CallType.Unknown
            )
            {
                IsReference = isReference,
            };

            return Match(TokenKind.EqualsGreater)
                ? ParseClosure(call)
                : call;
        }

        if (modulePath.Count == 0 &&
            (identifier.Value.StartsWith('$') || _scope.HasVariable(identifier.Value)))
        {
            return new VariableExpr(identifier);
        }

        var textArguments = ParseTextArguments();

        // Load alias if there is one
        var aliasResult = UseAliases ? _scope.ModuleScope.FindAlias(identifier.Value) : null;
        if (aliasResult != null)
        {
            identifier = identifier with { Value = aliasResult.Name };
            if (aliasResult.Value != null)
                textArguments.Insert(0, aliasResult.Value);
        }

        var plurality = ParsePlurality(identifier, out var newIdentifier);

        var textCall = new CallExpr(
            newIdentifier,
            modulePath,
            textArguments,
            CallStyle.TextArguments,
            plurality,
            CallType.Unknown
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
        if (identifier.Value.EndsWith("!"))
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
            return new();

        var pos = Current?.Position ?? TextPos.Default;
        var textArguments = new List<Expr>();
        var interpolationParts = new List<Expr>();
        var currentText = new StringBuilder();
        AdvanceIf(TokenKind.WhiteSpace);
        AdvanceIf(TokenKind.Backslash);

        while (!ReachedTextEnd())
        {
            AdvanceIf(TokenKind.Backslash);

            if (Previous?.Kind != TokenKind.Backslash && AdvanceIf(TokenKind.WhiteSpace))
            {
                var token = new Token(
                    TokenKind.StringLiteral,
                    currentText.ToString(),
                    pos
                );
                if (currentText.Length > 0)
                {
                    interpolationParts.Add(new LiteralExpr(token));
                    currentText.Clear();
                }

                textArguments.Add(new StringInterpolationExpr(interpolationParts, pos));
                interpolationParts = new();
                continue;
            }

            var next = Peek();
            var isStringLiteral = MatchInclWhiteSpace(TokenKind.StringLiteral);
            var isDollar = MatchInclWhiteSpace(TokenKind.Identifier) &&
                Current!.Value.StartsWith('$') &&
                Previous?.Value != "\\";
            if (MatchInclWhiteSpace(TokenKind.Tilde) &&
                (next == null || next.Kind is TokenKind.Slash or TokenKind.WhiteSpace))
            {
                Eat();
                currentText.Append(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
            else if (isStringLiteral || isDollar)
            {
                var stringToken = new Token(
                    TokenKind.StringLiteral,
                    currentText.ToString(),
                    pos
                );
                interpolationParts.Add(new LiteralExpr(stringToken));
                currentText.Clear();

                if (isStringLiteral)
                {
                    interpolationParts.Add(ParseStringLiteral());
                }
                else if (isDollar && Current!.Value.Length > 1)
                {
                    // Environment variable
                    interpolationParts.Add(new VariableExpr(Eat()));
                }
                else if (isDollar && next?.Kind == TokenKind.OpenBrace)
                {
                    Eat();
                    interpolationParts.Add(ParseBlock(StructureKind.Other));
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
                TokenKind.StringLiteral,
                currentText.ToString(),
                pos
            );
            interpolationParts.Add(new LiteralExpr(finalToken));
        }

        if (interpolationParts.Any())
        {
            textArguments.Add(new StringInterpolationExpr(interpolationParts, pos));
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

    private bool MatchIdentifier(string value)
        => Match(TokenKind.Identifier) && Current?.Value == value;

    private bool IsLiteral(TokenKind kind)
        => kind is
            TokenKind.IntegerLiteral or
            TokenKind.FloatLiteral or
            TokenKind.StringLiteral or
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