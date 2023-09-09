#region

using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting.Scope;
using Elk.Lexing;
using Elk.Std.Bindings;
using Elk.Std.DataTypes;
using Newtonsoft.Json;

#endregion

namespace Elk.Parsing;

enum StructureKind
{
    Other,
    Loop,
    Function,
    Module,
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

    public Expr? EnclosingFunction { get; set; }

    public RuntimeClosureFunction? EnclosingClosureValue
        => EnclosingFunction is ClosureExpr closureExpr
            ? closureExpr.RuntimeValue
            : null;

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

record Parameter(Token Identifier, Expr? DefaultValue, bool IsVariadic);

class ModuleExpr : Expr
{
    public Token Identifier { get; }

    public BlockExpr Body { get; }

    public ModuleExpr(Token identifier, BlockExpr body)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Body = body;
    }
}

class StructExpr : Expr
{
    public Token Identifier { get; }

    public IList<Parameter> Parameters { get; }

    public ModuleScope Module { get; }

    public StructExpr(
        Token identifier,
        IList<Parameter> parameters,
        ModuleScope module)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Parameters = parameters;
        Module = module;
    }
}

class FunctionExpr : Expr
{
    public Token Identifier { get; }

    public List<Parameter> Parameters { get; }

    public BlockExpr Block { get; set; }

    public ModuleScope Module { get; }

    public bool HasClosure { get; }

    public RuntimeClosureFunction? GivenClosure { get; set; }

    public FunctionExpr(
        Token identifier,
        List<Parameter> parameters,
        BlockExpr block,
        ModuleScope module,
        bool hasClosure)
        : base(identifier.Position)
    {
        Identifier = identifier;
        Parameters = parameters;
        Block = block;
        Module = module;
        HasClosure = hasClosure;
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

class NewExpr : Expr
{
    public Token Identifier { get; }

    public IList<Token> ModulePath { get; }

    public IList<Expr> Arguments { get; }

    public StructSymbol? StructSymbol { get; init; }

    public NewExpr(Token identifier, IList<Token> modulePath, IList<Expr> arguments)
        : base(identifier.Position)
    {
        Identifier = identifier;
        ModulePath = modulePath;
        Arguments = arguments;
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
    public IList<Expr> Values { get; }

    public ListExpr(IList<Expr> values, TextPos position)
        : base(position)
    {
        Values = values;
    }
}

class SetExpr : Expr
{
    public List<Expr> Entries { get; }

    public SetExpr(List<Expr> entries, TextPos position)
        : base(position)
    {
        Entries = entries;
    }
}

class DictionaryExpr : Expr
{
    public List<(Expr, Expr)> Entries { get; }

    public DictionaryExpr(List<(Expr, Expr)> entries, TextPos position)
        : base(position)
    {
        Entries = entries;
    }
}

class BlockExpr : Expr
{
    public List<Expr> Expressions { get; }

    public StructureKind ParentStructureKind { get; }

    public Scope Scope { get; set;  }

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

class FieldAccessExpr : Expr
{
    public Expr Object { get; }

    public Token Identifier { get; }

    public FieldAccessExpr(Expr objectExpr, Token identifier)
        : base(identifier.Position)
    {
        Object = objectExpr;
        Identifier = identifier;
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

class TypeExpr : Expr
{
    public Token Identifier { get; }

    public RuntimeType? RuntimeValue { get; init; }

    public TypeExpr(Token identifier)
        : base(identifier.Position)
    {
        Identifier = identifier;
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
    BuiltInError,
}

class CallExpr : Expr
{
    public Token Identifier { get; }

    public IList<Token> ModulePath { get; }

    public IList<Expr> Arguments { get; }

    public CallStyle CallStyle { get; }

    public Plurality Plurality { get; }

    public CallType CallType { get; }

    public FunctionSymbol? FunctionSymbol { get; init; }

    public StdFunction? StdFunction { get; init; }

    public Expr? PipedToProgram { get; init; }

    public CallExpr(
        Token identifier,
        IList<Token> modulePath,
        IList<Expr> arguments,
        CallStyle callStyle,
        Plurality plurality,
        CallType callType)
        : base(identifier.Position)
    {
        Identifier = identifier;
        ModulePath = modulePath;
        Arguments = arguments;
        CallStyle = callStyle;
        Plurality = plurality;
        CallType = callType;
    }
}

class LiteralExpr : Expr
{
    public Token Value { get; }

    public RuntimeObject? RuntimeValue { get; init; }

    public LiteralExpr(Token value)
        : base(value.Position)
    {
        Value = value;
    }
}

class FunctionReferenceExpr : Expr
{
    public Token Identifier { get; }

    public IList<Token> ModulePath { get; }

    public RuntimeFunction? RuntimeFunction { get; init; }

    public FunctionReferenceExpr(Token identifier, IList<Token> modulePath)
        : base(identifier.Position)
    {
        Identifier = identifier;
        ModulePath = modulePath;
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

    public BlockExpr Body { get; set; }

    public HashSet<string> CapturedVariables { get; init; } = new();

    public RuntimeClosureFunction? RuntimeValue { get; set; }

    public ClosureExpr(Expr function, List<Token> parameters, BlockExpr body)
        : base(body.Position)
    {
        Function = function;
        Parameters = parameters;
        Body = body;
    }
}