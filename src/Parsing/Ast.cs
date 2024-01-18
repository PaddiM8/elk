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

abstract class Expr(TextPos pos)
{
    [JsonIgnore]
    public TextPos Position { get; } = pos;

    public bool IsRoot { get; set; }

    public Expr? EnclosingFunction { get; set; }

    public RuntimeClosureFunction? EnclosingClosureValue
        => EnclosingFunction is ClosureExpr closureExpr
            ? closureExpr.RuntimeValue
            : null;
}

class EmptyExpr() : Expr(TextPos.Default);

record Parameter(Token Identifier, Expr? DefaultValue, bool IsVariadic);

class ModuleExpr(AccessLevel accessLevel, Token identifier, BlockExpr body) : Expr(identifier.Position)
{
    public AccessLevel AccessLevel { get; } = accessLevel;

    public Token Identifier { get; } = identifier;

    public BlockExpr Body { get; } = body;
}

class StructExpr(
    AccessLevel accessLevel,
    Token identifier,
    IList<Parameter> parameters,
    ModuleScope module)
    : Expr(identifier.Position)
{
    public AccessLevel AccessLevel { get; } = accessLevel;

    public Token Identifier { get; } = identifier;

    public IList<Parameter> Parameters { get; } = parameters;

    public ModuleScope Module { get; } = module;
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

class FunctionExpr(
    AccessLevel accessLevel,
    Token identifier,
    List<Parameter> parameters,
    BlockExpr block,
    ModuleScope module,
    bool hasClosure)
    : Expr(identifier.Position)
{
    public AccessLevel AccessLevel { get; } = accessLevel;

    public Token Identifier { get; } = identifier;

    public List<Parameter> Parameters { get; } = parameters;

    public BlockExpr Block { get; set; } = block;

    public ModuleScope Module { get; } = module;

    public bool HasClosure { get; } = hasClosure;

    public RuntimeClosureFunction? GivenClosure { get; set; }

    public AnalysisStatus AnalysisStatus { get; set; }
}

class LetExpr(List<Token> identifierList, Expr value) : Expr(identifierList.First().Position)
{
    public List<Token> IdentifierList { get; } = identifierList;

    public Expr Value { get; } = value;
}

class NewExpr(Token identifier, IList<Token> modulePath, IList<Expr> arguments)
    : Expr(identifier.Position)
{
    public Token Identifier { get; } = identifier;

    public IList<Token> ModulePath { get; } = modulePath;

    public IList<Expr> Arguments { get; } = arguments;

    public StructSymbol? StructSymbol { get; init; }
}

class IfExpr(Expr condition, Expr thenBranch, Expr? elseBranch) : Expr(condition.Position)
{
    public Expr Condition { get; } = condition;

    public Expr ThenBranch { get; } = thenBranch;

    public Expr? ElseBranch { get; } = elseBranch;
}

class ForExpr(List<Token> identifierList, Expr value, BlockExpr branch) : Expr(identifierList.First().Position)
{
    public List<Token> IdentifierList { get; } = identifierList;

    public Expr Value { get; } = value;

    public BlockExpr Branch { get; } = branch;
}

class WhileExpr(Expr condition, BlockExpr branch) : Expr(condition.Position)
{
    public Expr Condition { get; } = condition;

    public BlockExpr Branch { get; } = branch;
}

class TupleExpr(List<Expr> values, TextPos position) : Expr(position)
{
    public List<Expr> Values { get; } = values;
}

class ListExpr(IList<Expr> values, TextPos position) : Expr(position)
{
    public IList<Expr> Values { get; } = values;
}

class SetExpr(List<Expr> entries, TextPos position) : Expr(position)
{
    public List<Expr> Entries { get; } = entries;
}

class DictionaryExpr(List<(Expr, Expr)> entries, TextPos position) : Expr(position)
{
    public List<(Expr, Expr)> Entries { get; } = entries;
}

class BlockExpr(
    List<Expr> expressions,
    StructureKind parentStructureKind,
    TextPos pos,
    Scope scope)
    : Expr(pos)
{
    public List<Expr> Expressions { get; } = expressions;

    public StructureKind ParentStructureKind { get; } = parentStructureKind;

    public Scope Scope { get; set;  } = scope;
}

class KeywordExpr(TokenKind kind, Expr? value, TextPos pos) : Expr(pos)
{
    public TokenKind Kind { get; } = kind;

    public Expr? Value { get; } = value;
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

class FieldAccessExpr(Expr objectExpr, Token identifier) : Expr(identifier.Position)
{
    public Expr Object { get; } = objectExpr;

    public Token Identifier { get; } = identifier;

    public RuntimeString? RuntimeIdentifier { get; set; }
}

class RangeExpr(Expr? from, Expr? to, bool inclusive) : Expr(from?.Position ?? to!.Position)
{
    public Expr? From { get; } = from;

    public Expr? To { get; } = to;

    public bool Inclusive { get; } = inclusive;
}

class IndexerExpr(Expr value, Expr index) : Expr(index.Position)
{
    public Expr Value { get; } = value;

    public Expr Index { get; } = index;
}

class TypeExpr(Token identifier) : Expr(identifier.Position)
{
    public Token Identifier { get; } = identifier;

    public RuntimeType? RuntimeValue { get; init; }
}

class VariableExpr(Token identifier) : Expr(identifier.Position)
{
    public Token Identifier { get; } = identifier;
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

class CallExpr(
    Token identifier,
    IList<Token> modulePath,
    IList<Expr> arguments,
    CallStyle callStyle,
    Plurality plurality,
    CallType callType)
    : Expr(identifier.Position)
{
    public Token Identifier { get; } = identifier;

    public IList<Token> ModulePath { get; } = modulePath;

    public IList<Expr> Arguments { get; set; } = arguments;

    public CallStyle CallStyle { get; } = callStyle;

    public Plurality Plurality { get; } = plurality;

    public CallType CallType { get; } = callType;

    public FunctionSymbol? FunctionSymbol { get; init; }

    public StdFunction? StdFunction { get; init; }

    public Expr? PipedToProgram { get; init; }

    public RedirectionKind RedirectionKind { get; set; }

    public bool DisableRedirectionBuffering { get; set; }

    public bool AutomaticStart { get; set; } = true;

    public bool IsReference { get; set; }

    public Dictionary<string, Expr> EnvironmentVariables { get; init; } = new();
}

class LiteralExpr(Token value) : Expr(value.Position)
{
    public Token Value { get; } = value;

    public RuntimeObject? RuntimeValue { get; init; }
}

class StringInterpolationExpr : Expr
{
    public List<Expr> Parts { get; }

    public bool IsTextArgument { get; }

    public StringInterpolationExpr(List<Expr> parts, TextPos pos)
        : base(pos)
    {
        Parts = parts;
        IsTextArgument = parts.Any(x =>
            x is LiteralExpr
            {
                Value.Kind: TokenKind.TextArgumentStringLiteral,
            }
        );
    }
}

class ClosureExpr(CallExpr function, List<Token> parameters, BlockExpr body) : Expr(body.Position)
{
    public CallExpr Function { get; } = function;

    public List<Token> Parameters { get; } = parameters;

    public BlockExpr Body { get; set; } = body;

    public HashSet<string> CapturedVariables { get; init; } = [];

    public RuntimeClosureFunction? RuntimeValue { get; set; }
}

class TryExpr(BlockExpr body, BlockExpr catchBody, Token? catchIdentifier)
    : Expr(body.Position)
{
    public BlockExpr Body { get; } = body;

    public BlockExpr CatchBody { get; } = catchBody;

    public Token? CatchIdentifier { get; } = catchIdentifier;
}