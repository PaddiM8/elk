#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Std.DataTypes;
using Newtonsoft.Json;

#endregion

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

    public ModuleScope Module { get; }

    public bool HasClosure { get; }

    public bool IsAnalysed { get; }

    public FunctionExpr(
        Token identifier,
        List<Parameter> parameters,
        BlockExpr block,
        ModuleScope module,
        bool hasClosure,
        bool isAnalysed)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Parameters = parameters;
        Block = block;
        Module = module;
        HasClosure = hasClosure;
        IsAnalysed = isAnalysed;
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

    public BinaryExpr(Expr left, OperationKind op, Expr right)
        : base(left.Position)
    {
        Left = left;
        Operator = op;
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

    public UnaryExpr(OperationKind op, Expr value)
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

    public Token? ModuleName { get; }

    public VariableSymbol? VariableSymbol { get; set; }

    public VariableExpr(Token identifier, Token? moduleName = null)
        : base(identifier.Position)
    {
        Identifier = identifier;
        ModuleName = moduleName;
    }
}

class TypeExpr : Expr
{
    public Token Identifier { get; }

    public RuntimeType? RuntimeValue { get; set;  }

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

enum Plurality
{
    Singular,
    Plural,
}

enum CallType
{
    Unknown,
    Program,
    StdFunction,
    Function,
    BuiltInCd,
    BuiltInExec,
    BuiltInScriptPath,
    BuiltInClosure,
    BuiltInCall,
}

class CallExpr : Expr
{
    public Token Identifier { get; }

    public List<Expr> Arguments { get; }

    public CallStyle CallStyle { get; }

    public Plurality Plurality { get; }

    public CallType CallType { get; }

    public Token? ModuleName { get; }

    public FunctionSymbol? FunctionSymbol { get; init; }

    public MethodInfo? StdFunction { get; init; }

    public CallExpr(
        Token identifier,
        List<Expr> arguments,
        CallStyle callStyle,
        Plurality plurality,
        CallType callType,
        Token? moduleName = null)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Arguments = arguments;
        CallStyle = callStyle;
        Plurality = plurality;
        CallType = callType;
        ModuleName = moduleName;
    }
}

class FunctionReferenceExpr : Expr
{
    public Token Identifier { get; }

    public Token? ModuleName { get; }

    public RuntimeFunction? RuntimeFunction { get; init; }

    public FunctionReferenceExpr(Token identifier, Token? moduleName)
        : base(identifier.Position)
    {
        Identifier = identifier;
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

    public Scope Scope { get; }

    public BlockExpr(
        List<Expr> expressions,
        StructureKind parentStructureKind,
        TextPos pos,
        Scope scope)
        : base(pos)
    {
        Expressions = expressions;
        ParentStructureKind = parentStructureKind;
        Scope = scope;
    }
}

class LiteralExpr : Expr
{
    public Token Value { get; }

    public IRuntimeValue? RuntimeValue { get; set; }

    public LiteralExpr(Token value)
        : base(value.Position)
    {
        Value = value;
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

class ClosureExpr : Expr
{
    public Expr Function { get; }

    public List<Token> Parameters { get; }

    public BlockExpr Body { get; }

    public ClosureExpr(Expr function, List<Token> parameters, BlockExpr body)
        : base(body.Position)
    {
        Function = function;
        Parameters = parameters;
        Body = body;
    }
}