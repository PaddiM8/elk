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
    PipeErr,
    PipeAll,
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
    public AccessLevel AccessLevel { get; }

    public Token Identifier { get; }

    public BlockExpr Body { get; }

    public ModuleExpr(AccessLevel accessLevel, Token identifier, BlockExpr body)
        : base(identifier.Position)
    {
        AccessLevel = accessLevel;
        Identifier = identifier;
        Body = body;
    }
}

class StructExpr : Expr
{
    public AccessLevel AccessLevel { get; }

    public Token Identifier { get; }

    public IList<Parameter> Parameters { get; }

    public ModuleScope Module { get; }

    public StructExpr(
        AccessLevel accessLevel,
        Token identifier,
        IList<Parameter> parameters,
        ModuleScope module)
        : base(identifier.Position)
    {
        AccessLevel = accessLevel;
        Identifier = identifier;
        Parameters = parameters;
        Module = module;
    }
}

enum AnalysisStatus
{
    None,
    Failed,
    Analysed,
    Evaluated,
}

enum AccessLevel
{
    Private,
    Public,
}

class FunctionExpr : Expr
{
    public AccessLevel AccessLevel { get; }

    public Token Identifier { get; }

    public List<Parameter> Parameters { get; }

    public BlockExpr Block { get; set; }

    public ModuleScope Module { get; }

    public bool HasClosure { get; }

    public RuntimeClosureFunction? GivenClosure { get; set; }

    public AnalysisStatus AnalysisStatus { get; set; }

    public FunctionExpr(
        AccessLevel accessLevel,
        Token identifier,
        List<Parameter> parameters,
        BlockExpr block,
        ModuleScope module,
        bool hasClosure)
        : base(identifier.Position)
    {
        AccessLevel = accessLevel;
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

    public RuntimeString? RuntimeIdentifier { get; set; }

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

public enum Plurality
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
    BuiltInTime,
}

enum RedirectionKind
{
    None,
    Output,
    Error,
    All,
}

class CallExpr : Expr
{
    public Token Identifier { get; }

    public IList<Token> ModulePath { get; }

    public IList<Expr> Arguments { get; set; }

    public CallStyle CallStyle { get; }

    public Plurality Plurality { get; }

    public CallType CallType { get; }

    public FunctionSymbol? FunctionSymbol { get; init; }

    public StdFunction? StdFunction { get; init; }

    public Expr? PipedToProgram { get; init; }

    public RedirectionKind RedirectionKind { get; set; }

    public bool DisableRedirectionBuffering { get; set; }

    public bool AutomaticStart { get; set; } = true;

    public bool IsReference { get; set; }

    public Dictionary<string, Expr> EnvironmentVariables { get; init; } = new();

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
    public CallExpr Function { get; }

    public List<Token> Parameters { get; }

    public BlockExpr Body { get; set; }

    public HashSet<string> CapturedVariables { get; init; } = new();

    public RuntimeClosureFunction? RuntimeValue { get; set; }

    public ClosureExpr(CallExpr function, List<Token> parameters, BlockExpr body)
        : base(body.Position)
    {
        Function = function;
        Parameters = parameters;
        Body = body;
    }
}

class TryExpr : Expr
{
    public BlockExpr Body { get; }

    public BlockExpr CatchBody { get; }

    public Token? CatchIdentifier { get; }

    public TryExpr(BlockExpr body, BlockExpr catchBody, Token? catchIdentifier)
        : base(body.Position)
    {
        Body = body;
        CatchBody = catchBody;
        CatchIdentifier = catchIdentifier;
    }
}

class ThrowExpr : Expr
{
    public Expr Value { get; }

    public ThrowExpr(Expr value)
        : base(value.Position)
    {
        Value = value;
    }
}