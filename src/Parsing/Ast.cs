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

public enum StructureKind
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

public abstract class Expr(TextPos pos)
{
    [JsonIgnore]
    public TextPos Position { get; } = pos;

    public bool IsRoot { get; set; }

    public Expr? EnclosingFunction { get; set; }

    internal RuntimeClosureFunction? EnclosingClosureValue
        => EnclosingFunction is ClosureExpr closureExpr
            ? closureExpr.RuntimeValue
            : null;
}

public class EmptyExpr() : Expr(TextPos.Default);

public record Parameter(Token Identifier, Expr? DefaultValue, bool IsVariadic);

public class ModuleExpr(AccessLevel accessLevel, Token identifier, BlockExpr body) : Expr(identifier.Position)
{
    public AccessLevel AccessLevel { get; } = accessLevel;

    public Token Identifier { get; } = identifier;

    public BlockExpr Body { get; } = body;
}

public class StructExpr(
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

internal enum AnalysisStatus
{
    None,
    Failed,
    Analysed,
    Evaluated,
}

public enum AccessLevel
{
    Private,
    Public,
}

public class FunctionExpr(
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

    internal RuntimeClosureFunction? GivenClosure { get; set; }

    internal AnalysisStatus AnalysisStatus { get; set; }
}

public class LetExpr(List<Token> identifierList, Expr value) : Expr(identifierList.First().Position)
{
    public List<Token> IdentifierList { get; } = identifierList;

    public Expr Value { get; } = value;
}

public class NewExpr(Token identifier, IList<Token> modulePath, IList<Expr> arguments)
    : Expr(identifier.Position)
{
    public Token Identifier { get; } = identifier;

    public IList<Token> ModulePath { get; } = modulePath;

    public IList<Expr> Arguments { get; } = arguments;

    public StructSymbol? StructSymbol { get; init; }
}

public class IfExpr(Expr condition, Expr thenBranch, Expr? elseBranch) : Expr(condition.Position)
{
    public Expr Condition { get; } = condition;

    public Expr ThenBranch { get; } = thenBranch;

    public Expr? ElseBranch { get; } = elseBranch;
}

public class ForExpr(List<Token> identifierList, Expr value, BlockExpr branch) : Expr(identifierList.First().Position)
{
    public List<Token> IdentifierList { get; } = identifierList;

    public Expr Value { get; } = value;

    public BlockExpr Branch { get; } = branch;
}

public class WhileExpr(Expr condition, BlockExpr branch) : Expr(condition.Position)
{
    public Expr Condition { get; } = condition;

    public BlockExpr Branch { get; } = branch;
}

public class TupleExpr(List<Expr> values, TextPos position) : Expr(position)
{
    public List<Expr> Values { get; } = values;
}

public class ListExpr(IList<Expr> values, TextPos position) : Expr(position)
{
    public IList<Expr> Values { get; } = values;
}

public class SetExpr(List<Expr> entries, TextPos position) : Expr(position)
{
    public List<Expr> Entries { get; } = entries;
}

public class DictionaryExpr(List<(Expr, Expr)> entries, TextPos position) : Expr(position)
{
    public List<(Expr, Expr)> Entries { get; } = entries;
}

public class BlockExpr(
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

public class KeywordExpr(TokenKind kind, Expr? value, TextPos pos) : Expr(pos)
{
    public TokenKind Kind { get; } = kind;

    public Expr? Value { get; } = value;
}

public class BinaryExpr : Expr
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

public class UnaryExpr : Expr
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

public class FieldAccessExpr(Expr objectExpr, Token identifier) : Expr(identifier.Position)
{
    public Expr Object { get; } = objectExpr;

    public Token Identifier { get; } = identifier;

    public RuntimeString? RuntimeIdentifier { get; set; }
}

public class RangeExpr(Expr? from, Expr? to, bool inclusive) : Expr(from?.Position ?? to!.Position)
{
    public Expr? From { get; } = from;

    public Expr? To { get; } = to;

    public bool Inclusive { get; } = inclusive;
}

public class IndexerExpr(Expr value, Expr index) : Expr(index.Position)
{
    public Expr Value { get; } = value;

    public Expr Index { get; } = index;
}

public class TypeExpr(Token identifier) : Expr(identifier.Position)
{
    public Token Identifier { get; } = identifier;

    public RuntimeType? RuntimeValue { get; init; }
}

public class VariableExpr(Token identifier) : Expr(identifier.Position)
{
    public Token Identifier { get; } = identifier;
}

public enum CallStyle
{
    Parenthesized,
    TextArguments,
}

public enum Plurality
{
    Singular,
    Plural,
}

public enum CallType
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

public enum RedirectionKind
{
    None,
    Output,
    Error,
    All,
}

public class CallExpr(
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

public class LiteralExpr(Token value) : Expr(value.Position)
{
    public Token Value { get; } = value;

    public RuntimeObject? RuntimeValue { get; init; }
}

public class StringInterpolationExpr : Expr
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

public class ClosureExpr(CallExpr function, List<Token> parameters, BlockExpr body) : Expr(body.Position)
{
    public CallExpr Function { get; } = function;

    public List<Token> Parameters { get; } = parameters;

    public BlockExpr Body { get; set; } = body;

    public HashSet<string> CapturedVariables { get; init; } = [];

    internal RuntimeClosureFunction? RuntimeValue { get; set; }
}

public class TryExpr(BlockExpr body, BlockExpr catchBody, Token? catchIdentifier)
    : Expr(body.Position)
{
    public BlockExpr Body { get; } = body;

    public BlockExpr CatchBody { get; } = catchBody;

    public Token? CatchIdentifier { get; } = catchIdentifier;
}