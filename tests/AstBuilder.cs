#region

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Scoping;

#endregion

// ReSharper disable UnusedMember.Global

namespace Elk.Tests;

static class AstBuilder
{
    private static readonly Scope _scope = new RootModuleScope("/", null);

    public static Token Token(TokenKind kind, string value)
        => new(kind, value, TextPos.Default);

    public static FunctionExpr Function(
        string identifier,
        IEnumerable<string> parameters,
        BlockExpr block,
        ModuleScope module,
        bool hasClosure)
        => new(
            AccessLevel.Public,
            Token(TokenKind.Identifier, identifier),
            parameters.Select(x => new Parameter(Token(TokenKind.Identifier, x), null, false)).ToList(),
            block,
            module,
            hasClosure
        );

    public static LetExpr Let(string identifier, Expr value)
        => new([Token(TokenKind.Identifier, identifier)], value, _scope, value.StartPosition);

    public static KeywordExpr KeywordExpr(TokenKind kind, Expr value)
        => new(Token(kind, kind.ToString()), value, _scope);

    public static BinaryExpr Binary(Expr left, TokenKind op, Expr right)
        => new(left, op, right, _scope);

    public static UnaryExpr Unary(TokenKind op, Expr value)
        => new(op, value, _scope);

    public static VariableExpr Var(string identifier)
        => new(Token(TokenKind.Identifier, identifier), _scope);

    public static CallExpr Call(string identifier, List<Expr> arguments)
        => new(
            Token(TokenKind.Identifier, identifier),
            Array.Empty<Token>(),
            arguments,
            CallStyle.Parenthesized,
            CallType.Unknown,
            _scope,
            TextPos.Default
        );

    public static IfExpr If(Expr condition, Expr thenBranch, Expr? elseBranch = null)
        => new(condition, thenBranch, elseBranch, _scope);

    public static BlockExpr Block(List<Expr> expressions, StructureKind structureKind, Scope scope)
        => new(expressions, structureKind, scope, TextPos.Default, TextPos.Default);

    public static LiteralExpr Literal(object value)
        => value switch
        {
            null => new(Token(TokenKind.Nil, "nil"), _scope),
            true => new(Token(TokenKind.True, "true"), _scope),
            false => new(Token(TokenKind.False, "false"), _scope),
            int x => new LiteralExpr(
                Token(
                    TokenKind.IntegerLiteral,
                    x.ToString()
                ),
                _scope
            ),
            double x => new LiteralExpr(
                Token(
                    TokenKind.FloatLiteral,
                    x.ToString(CultureInfo.InvariantCulture)
                ),
                _scope
            ),
            string x => new(
                Token(TokenKind.DoubleQuoteStringLiteral, x),
                _scope
            ),
            _ => new(
                Token(TokenKind.Unknown, value.ToString() ?? ""),
                _scope
            ),
        };
}