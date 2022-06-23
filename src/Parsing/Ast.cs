using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Newtonsoft.Json;

namespace Elk.Parsing;

enum StructureKind
{
    Other,
    Loop,
    Function,
}

public enum OperationKind
{
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Modulo,
    Power,
    Greater,
    GreaterEquals,
    Less,
    LessEquals,
    Equals,
    EqualsEquals,
    NotEquals,
    And,
    Or,
    Not,
    Pipe,
    If,
    Coalescing,
    NonRedirectingAnd,
    NonRedirectingOr,
    In,
}

abstract class Expr
{
    [JsonIgnore]
    public TextPos Position { get; }

    public bool IsRoot { get; set; }

    protected Expr(TextPos pos)
    {
        Position = pos;
    }
}

class EmptyExpr : Expr
{
    public EmptyExpr()
        : base(TextPos.Default)
    {
    }
}

record Parameter(Token Identifier, Expr? DefaultValue, bool Variadic);

class FunctionExpr : Expr
{
    public Token Identifier { get; }

    public List<Parameter> Parameters { get; }

    public BlockExpr Block { get; }

    public ModuleScope Module { get;  }

    public FunctionExpr(Token identifier, List<Parameter> parameters, BlockExpr block, ModuleScope module)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Parameters = parameters;
        Block = block;
        Module = module;
    }
}

class LetExpr : Expr
{
    public List<Token> IdentifierList { get; }

    public Expr Value { get; }

    public LetExpr(List<Token> identifierList, Expr value)
        : base(identifierList.First().Position)
    {
        IdentifierList = identifierList;
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

    public OperationKind Operator { get; }

    public Expr Right { get; }
    
    public BinaryExpr(Expr left, TokenKind op, Expr right)
        : base(left.Position)
    {
        Left = left;
        Operator = op.ToOperationKind();
        Right = right;
    }
}

class UnaryExpr : Expr
{
    public OperationKind Operator { get; }

    public Expr Value { get; }
    
    public UnaryExpr(TokenKind op, Expr value)
        : base(value.Position)
    {
        Operator = op.ToOperationKind();
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

    public Token? ModuleName { get; }

    public VariableExpr(Token identifier, Token? moduleName = null)
        : base(identifier.Position)
    {
        Identifier = identifier;
        ModuleName = moduleName;
    }
}

class TypeExpr : Expr
{
    public Token Identifier { get;  }

    public TypeExpr(Token identifier)
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

    public Token? ModuleName { get; }

    public CallExpr(Token identifier, List<Expr> arguments, CallStyle callStyle, Token? moduleName = null)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Arguments = arguments;
        CallStyle = callStyle;
        ModuleName = moduleName;
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
    public List<Token> IdentifierList { get; }

    public Expr Value { get; }

    public BlockExpr Branch { get; }

    public ForExpr(List<Token> identifierList, Expr value, BlockExpr branch)
        : base(identifierList.First().Position)
    {
        IdentifierList = identifierList;
        Value = value;
        Branch = branch;
    }
}

class WhileExpr : Expr
{
    public Expr Condition { get; }

    public BlockExpr Branch { get; }

    public WhileExpr(Expr condition, BlockExpr branch)
        : base(condition.Position)
    {
        Condition = condition;
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

class StringInterpolationExpr : Expr
{
    public List<Expr> Parts { get; }

    public StringInterpolationExpr(List<Expr> parts, TextPos pos)
        : base(pos)
    {
        Parts = parts;
    }
}