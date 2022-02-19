using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Shel.Lexing;

namespace Shel;

abstract class Expr
{
    [JsonIgnore]
    public TextPos Position { get; }

    public Expr(TextPos pos)
    {
        Position = pos;
    }
}

class LetExpr : Expr
{
    public Token Identifier { get; }

    public Expr Value { get; }

    public LetExpr(Token identifier, Expr value)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Value = value;
    }
}

class LiteralExpr : Expr
{
    public Token Value { get; }

    public LiteralExpr(Token value)
        : base(value.Position)
    {
        Value = value;
    }
}

class BinaryExpr : Expr
{
    public Expr Left { get; }

    public TokenKind Operator { get; }

    public Expr Right { get; }

    public BinaryExpr(Expr left, TokenKind op, Expr right)
        : base(left.Position)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}

class UnaryExpr : Expr
{
    public TokenKind Operator { get; }

    public Expr Value { get; }

    public UnaryExpr(TokenKind op, Expr value)
        : base(value.Position)
    {
        Operator = op;
        Value = value;
    }
}

class VariableExpr : Expr
{
    public Token Identifier { get; }

    public VariableExpr(Token identifier)
        : base(identifier.Position)
    {
        Identifier = identifier;
    }
}

class CallExpr : Expr
{
    public Token Identifier { get; }

    public List<Expr> Arguments { get; }

    public CallExpr(Token identifier, List<Expr> arguments)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Arguments = arguments;
    }
}