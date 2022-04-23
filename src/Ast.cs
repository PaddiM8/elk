using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Elk.Lexing;

namespace Elk;

enum StructureKind
{
    Other,
    Loop,
    Function,
}

abstract class Expr
{
    [JsonIgnore]
    public TextPos Position { get; }

    public bool IsRoot { get; set; }

    public Expr(TextPos pos)
    {
        Position = pos;
    }
}

class EmptyExpr : Expr
{
    public EmptyExpr()
        : base(new(0, 0))
    {
    }
}

class FunctionExpr : Expr
{
    public Token Identifier { get; }

    public List<Token> Parameters { get; }

    public BlockExpr Block { get; }

    public FunctionExpr(Token identifier, List<Token> parameters, BlockExpr block)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Parameters = parameters;
        Block = block;
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

class KeywordExpr : Expr
{
    public TokenKind Kind { get; }

    public Expr? Value { get; }

    public KeywordExpr(TokenKind kind, Expr? value, TextPos pos)
        : base(pos)
    {
        Kind = kind;
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

class RangeExpr : Expr
{
    public Expr? From { get; }

    public Expr? To { get; }

    public bool Inclusive { get; }

    public RangeExpr(Expr? from, Expr? to, bool inclusive)
        : base(from?.Position ?? to!.Position)
    {
        From = from;
        To = to;
        Inclusive = inclusive;
    }
}

class IndexerExpr : Expr
{
    public Expr Value { get; }

    public Expr Index { get; }

    public IndexerExpr(Expr value, Expr index)
        : base(index.Position)
    {
        Value = value;
        Index = index;
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

enum CallStyle
{
    Parenthesized,
    TextArguments,
}

class CallExpr : Expr
{
    public Token Identifier { get; }

    public List<Expr> Arguments { get; }

    public CallStyle CallStyle { get; }

    public CallExpr(Token identifier, List<Expr> arguments, CallStyle callStyle)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Arguments = arguments;
        CallStyle = callStyle;
    }
}

class IfExpr : Expr
{
    public Expr Condition { get; }

    public Expr ThenBranch { get; }

    public Expr? ElseBranch { get; }

    public IfExpr(Expr condition, Expr thenBranch, Expr? elseBranch)
        : base(condition.Position)
    {
        Condition = condition;
        ThenBranch = thenBranch;
        ElseBranch = elseBranch;
    }
}

class ForExpr : Expr
{
    public Token Identifier { get; }

    public Expr Value { get; }

    public BlockExpr Branch { get; }

    public ForExpr(Token identifier, Expr value, BlockExpr branch)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Value = value;
        Branch = branch;
    }
}

class TupleExpr : Expr
{
    public List<Expr> Values { get; }

    public TupleExpr(List<Expr> values, TextPos position)
        : base(position)
    {
        Values = values;
    }
}

class ListExpr : Expr
{
    public List<Expr> Values { get; }

    public ListExpr(List<Expr> values, TextPos position)
        : base(position)
    {
        Values = values;
    }
}

class DictionaryExpr : Expr
{
    public List<(string, Expr)> Entries { get; }

    public DictionaryExpr(List<(string, Expr)> entries, TextPos position)
        : base(position)
    {
        Entries = entries;
    }
}

class BlockExpr : Expr
{
    public List<Expr> Expressions { get; }

    public StructureKind ParentStructureKind { get; }

    public BlockExpr(List<Expr> expressions, StructureKind parentStructureKind, TextPos pos)
        : base(pos)
    {
        Expressions = expressions;
        ParentStructureKind = parentStructureKind;
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

class IntegerLiteralExpr : LiteralExpr
{
    public int NumberValue { get; }

    public IntegerLiteralExpr(Token value)
        : base(value)
    {
        NumberValue = int.Parse(value.Value);
    }
}

class FloatLiteralExpr : LiteralExpr
{
    public double NumberValue { get; }

    public FloatLiteralExpr(Token value)
        : base(value)
    {
        NumberValue = double.Parse(value.Value);
    }
}