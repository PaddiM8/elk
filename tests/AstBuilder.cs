using System.Collections.Generic;
using System.Linq;
using Elk.Lexing;
// ReSharper disable UnusedMember.Global

namespace Elk.Tests;

static class AstBuilder
{
    public static Token Token(TokenKind kind, string value)
        => new(kind, value, new TextPos(0, 0));

    public static FunctionExpr Function(string identifier, IEnumerable<string> parameters, BlockExpr block)
        => new(
            Token(TokenKind.Identifier, identifier),
            parameters.Select(x => Token(TokenKind.Identifier, x)).ToList(),
            block
        );

    public static LetExpr Let(string identifier, Expr value)
        => new(Token(TokenKind.Identifier, identifier), value);

    public static KeywordExpr KeywordExpr(TokenKind kind, Expr value)
        => new(kind, value, new(0, 0));

    public static BinaryExpr Binary(Expr left, TokenKind op, Expr right)
        => new(left, op, right);

    public static UnaryExpr Unary(TokenKind op, Expr value)
        => new(op, value);

    public static VariableExpr Var(string identifier)
        => new(Token(TokenKind.Identifier, identifier));

    public static CallExpr Call(string identifier, List<Expr> arguments)
        => new(Token(TokenKind.Identifier, identifier), arguments, CallStyle.Parenthesized);

    public static IfExpr If(Expr condition, Expr thenBranch, Expr? elseBranch = null)
        => new(condition, thenBranch, elseBranch);

    public static BlockExpr Block(List<Expr> expressions, StructureKind structureKind)
        => new(expressions, structureKind, new TextPos(0, 0));

    public static LiteralExpr Literal(object value)
        => value switch
        {
            null => new(Token(TokenKind.Nil, "nil")),
            true => new(Token(TokenKind.True, "true")),
            false => new(Token(TokenKind.False, "false")),
            int x => new IntegerLiteralExpr(Token(TokenKind.IntegerLiteral, x.ToString())),
            double x => new FloatLiteralExpr(Token(TokenKind.FloatLiteral, x.ToString())),
            string x => new(Token(TokenKind.StringLiteral, x)),
            _ => new(Token(TokenKind.Unknown, value.ToString() ?? "")),
        };
}