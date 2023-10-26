#region

using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Parsing;

#endregion

// ReSharper disable UnusedMember.Global

namespace Elk.Tests;

static class AstBuilder
{
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
        => new(new() { Token(TokenKind.Identifier, identifier) }, value);

    public static KeywordExpr KeywordExpr(TokenKind kind, Expr value)
        => new(kind, value, TextPos.Default);

    public static BinaryExpr Binary(Expr left, TokenKind op, Expr right)
        => new(left, op, right);

    public static UnaryExpr Unary(TokenKind op, Expr value)
        => new(op, value);

    public static VariableExpr Var(string identifier)
        => new(Token(TokenKind.Identifier, identifier));

    public static CallExpr Call(string identifier, List<Expr> arguments)
        => new(
            Token(TokenKind.Identifier, identifier),
            Array.Empty<Token>(),
            arguments,
            CallStyle.Parenthesized,
            Plurality.Singular,
            CallType.Unknown
        );

    public static IfExpr If(Expr condition, Expr thenBranch, Expr? elseBranch = null)
        => new(condition, thenBranch, elseBranch);

    public static BlockExpr Block(List<Expr> expressions, StructureKind structureKind, Scope scope)
        => new(expressions, structureKind, TextPos.Default, scope);

    public static LiteralExpr Literal(object value)
        => value switch
        {
            null => new(Token(TokenKind.Nil, "nil")),
            true => new(Token(TokenKind.True, "true")),
            false => new(Token(TokenKind.False, "false")),
            int x => new LiteralExpr(Token(TokenKind.IntegerLiteral, x.ToString())),
            double x => new LiteralExpr(Token(TokenKind.FloatLiteral, x.ToString())),
            string x => new(Token(TokenKind.StringLiteral, x)),
            _ => new(Token(TokenKind.Unknown, value.ToString() ?? "")),
        };
}