#region

using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Lexing;
using Elk.Scoping;
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

public abstract class Expr(TextPos startPos, TextPos endPos, Scope scope)
{
    [JsonIgnore]
    public TextPos StartPosition { get; } = startPos;

    public TextPos EndPosition { get; } = endPos;

    public bool IsRoot { get; set; }

    public Expr? EnclosingFunction { get; set; }

    public Scope Scope { get; } = scope;

    public abstract IEnumerable<Expr> ChildExpressions { get; }
}

public class EmptyExpr(Scope scope) : Expr(TextPos.Default, TextPos.Default, scope)
{
    public override IEnumerable<Expr> ChildExpressions { get; } = Array.Empty<Expr>();
}

public record Parameter(Token Identifier, Expr? DefaultValue, bool IsVariadic);

public class ModuleExpr(
    AccessLevel accessLevel,
    Token identifier,
    BlockExpr body)
    : Expr(identifier.Position, body.EndPosition, body.Scope)
{
    public AccessLevel AccessLevel { get; } = accessLevel;

    public Token Identifier { get; } = identifier;

    public BlockExpr Body { get; } = body;

    public override IEnumerable<Expr> ChildExpressions
        => [Body];
}

public class StructExpr(
    AccessLevel accessLevel,
    Token identifier,
    IList<Parameter> parameters,
    ModuleScope module,
    TextPos startPos,
    TextPos endPos)
    : Expr(startPos, endPos, module)
{
    public AccessLevel AccessLevel { get; } = accessLevel;

    public Token Identifier { get; } = identifier;

    public IList<Parameter> Parameters { get; } = parameters;

    public ModuleScope Module { get; } = module;

    public override IEnumerable<Expr> ChildExpressions { get; } = Array.Empty<Expr>();
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
    : Expr(identifier.Position, block.EndPosition, block.Scope)
{
    public AccessLevel AccessLevel { get; } = accessLevel;

    public Token Identifier { get; } = identifier;

    public List<Parameter> Parameters { get; } = parameters;

    public BlockExpr Block { get; set; } = block;

    public ModuleScope Module { get; } = module;

    public bool HasClosure { get; } = hasClosure;

    public VariableSymbol? ClosureSymbol { get; init; }

    public override IEnumerable<Expr> ChildExpressions
        => [Block];

    internal AnalysisStatus AnalysisStatus { get; set; }
}

public class LetExpr(
    List<Token> identifierList,
    Expr value,
    Scope scope,
    TextPos startPos)
    : Expr(startPos, value.EndPosition, scope)
{
    public List<Token> IdentifierList { get; } = identifierList;

    public Expr Value { get; } = value;

    public IEnumerable<VariableSymbol> Symbols
        => IdentifierList
            .Select(x => Scope.FindVariable(x.Value))
            .Where(x => x != null)!;

    public override IEnumerable<Expr> ChildExpressions
        => [Value];
}

public class NewExpr(
    Token identifier,
    IList<Token> modulePath,
    IList<Expr> arguments,
    Scope scope,
    TextPos startPos,
    TextPos endPos)
    : Expr(startPos, endPos, scope)
{
    public Token Identifier { get; } = identifier;

    public IList<Token> ModulePath { get; } = modulePath;

    public IList<Expr> Arguments { get; } = arguments;

    public StructSymbol? StructSymbol { get; init; }

    public override IEnumerable<Expr> ChildExpressions
        => Arguments;
}

public class IfExpr(
    Expr condition,
    Expr thenBranch,
    Expr? elseBranch,
    Scope scope)
    : Expr(condition.StartPosition, elseBranch?.EndPosition ?? thenBranch.EndPosition, scope)
{
    public Expr Condition { get; } = condition;

    public Expr ThenBranch { get; } = thenBranch;

    public Expr? ElseBranch { get; } = elseBranch;

    public override IEnumerable<Expr> ChildExpressions
        => new List<Expr?> { Condition, ThenBranch, ElseBranch }
            .Where(x => x != null)!;
}

public class ForExpr(
    List<Token> identifierList,
    Expr value,
    BlockExpr branch,
    Scope scope)
    : Expr(value.StartPosition, branch.EndPosition, scope)
{
    public List<Token> IdentifierList { get; } = identifierList;

    public Expr Value { get; } = value;

    public BlockExpr Branch { get; } = branch;

    public override IEnumerable<Expr> ChildExpressions
        => [Value, Branch];
}

public class WhileExpr(Expr condition, BlockExpr branch, Scope scope)
    : Expr(condition.StartPosition, branch.EndPosition, scope)
{
    public Expr Condition { get; } = condition;

    public BlockExpr Branch { get; } = branch;

    public override IEnumerable<Expr> ChildExpressions
        => [Condition, Branch];
}

public class TupleExpr(List<Expr> values, Scope scope, TextPos startPos, TextPos endPos)
    : Expr(startPos, endPos, scope)
{
    public List<Expr> Values { get; } = values;

    public override IEnumerable<Expr> ChildExpressions
        => Values;
}

public class ListExpr(IList<Expr> values, Scope scope, TextPos startPos, TextPos endPos)
    : Expr(startPos, endPos, scope)
{
    public IList<Expr> Values { get; } = values;

    public override IEnumerable<Expr> ChildExpressions
        => Values;
}

public class SetExpr(List<Expr> entries, Scope scope, TextPos startPos, TextPos endPos)
    : Expr(startPos, endPos, scope)
{
    public List<Expr> Entries { get; } = entries;

    public override IEnumerable<Expr> ChildExpressions
        => Entries;
}

public class DictionaryExpr(
    List<(Expr, Expr)> entries,
    Scope scope,
    TextPos startPos,
    TextPos endPos)
    : Expr(startPos, endPos, scope)
{
    public List<(Expr, Expr)> Entries { get; } = entries;

    public override IEnumerable<Expr> ChildExpressions
        => Entries.SelectMany(x => new List<Expr> { x.Item1, x.Item2 });
}

public class BlockExpr(
    List<Expr> expressions,
    StructureKind parentStructureKind,
    Scope scope,
    TextPos startPos,
    TextPos endPos)
    : Expr(startPos, endPos, scope)
{
    public List<Expr> Expressions { get; } = expressions;

    public StructureKind ParentStructureKind { get; } = parentStructureKind;

    public override IEnumerable<Expr> ChildExpressions
        => Expressions;
}

public class KeywordExpr(Token keyword, Expr? value, Scope scope)
    : Expr(keyword.Position, value?.EndPosition ?? keyword.EndPosition, scope)
{
    public Token Keyword { get; } = keyword;

    public Expr? Value { get; } = value;

    public override IEnumerable<Expr> ChildExpressions
        => Value == null
            ? Array.Empty<Expr>()
            : [Value];
}

public class BinaryExpr : Expr
{
    public Expr Left { get; }

    public OperationKind Operator { get; }

    public Expr Right { get; }

    public override IEnumerable<Expr> ChildExpressions
        => [Left, Right];

    public BinaryExpr(Expr left, TokenKind op, Expr right, Scope scope)
        : base(left.StartPosition, right.EndPosition, scope)
    {
        Left = left;
        Operator = op.ToOperationKind();
        Right = right;
    }

    public BinaryExpr(Expr left, OperationKind op, Expr right, Scope scope)
        : base(left.StartPosition, right.EndPosition, scope)
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

    public override IEnumerable<Expr> ChildExpressions
        => [Value];

    public UnaryExpr(TokenKind op, Expr value, Scope scope)
        : base(value.StartPosition, value.EndPosition, scope)
    {
        Operator = op.ToOperationKind();
        Value = value;
    }

    public UnaryExpr(OperationKind op, Expr value, Scope scope)
        : base(value.StartPosition, value.EndPosition, scope)
    {
        Operator = op;
        Value = value;
    }
}

public class FieldAccessExpr(Expr objectExpr, Token identifier, Scope scope)
    : Expr(objectExpr.StartPosition, identifier.EndPosition, scope)
{
    public Expr Object { get; } = objectExpr;

    public Token Identifier { get; } = identifier;

    public RuntimeString? RuntimeIdentifier { get; set; }

    public override IEnumerable<Expr> ChildExpressions
        => [Object];
}

public class RangeExpr(
    Expr? from,
    Expr? to,
    bool inclusive,
    Scope scope)
    : Expr(from?.StartPosition ?? to!.StartPosition, to?.EndPosition ?? from!.EndPosition, scope)
{
    public Expr? From { get; } = from;

    public Expr? To { get; } = to;

    public bool Inclusive { get; } = inclusive;

    public override IEnumerable<Expr> ChildExpressions
        => new List<Expr?> { From, To }.Where(x => x != null)!;
}

public class IndexerExpr(Expr value, Expr index, Scope scope)
    : Expr(value.StartPosition, index.EndPosition, scope)
{
    public Expr Value { get; } = value;

    public Expr Index { get; } = index;

    public override IEnumerable<Expr> ChildExpressions
        => [Value, Index];
}

public class TypeExpr(Token identifier, Scope scope)
    : Expr(identifier.Position, identifier.EndPosition, scope)
{
    public Token Identifier { get; } = identifier;

    public RuntimeType? RuntimeValue { get; init; }

    public override IEnumerable<Expr> ChildExpressions
        => Array.Empty<Expr>();
}

public class VariableExpr(Token identifier, Scope scope)
    : Expr(identifier.Position, identifier.EndPosition, scope)
{
    public Token Identifier { get; } = identifier;

    public override IEnumerable<Expr> ChildExpressions
        => Array.Empty<Expr>();
}

public enum CallStyle
{
    Parenthesized,
    TextArguments,
}

public enum CallType
{
    Unknown,
    Program,
    StdFunction,
    Function,
    BuiltInExec,
    BuiltInClosure,
    BuiltInCall,
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
    CallType callType,
    Scope scope,
    TextPos endPos)
    : Expr(
        modulePath.FirstOrDefault()?.Position ?? identifier.Position,
        endPos,
        scope
    )
{
    public Token Identifier { get; } = identifier;

    public IList<Token> ModulePath { get; } = modulePath;

    public IList<Expr> Arguments { get; set; } = arguments;

    public CallStyle CallStyle { get; } = callStyle;

    public CallType CallType { get; } = callType;

    public FunctionSymbol? FunctionSymbol { get; init; }

    public StdFunction? StdFunction { get; init; }

    public Expr? PipedToProgram { get; init; }

    public RedirectionKind RedirectionKind { get; set; }

    public bool DisableRedirectionBuffering { get; set; }

    public bool AutomaticStart { get; set; } = true;

    public bool IsReference { get; set; }

    public Dictionary<string, Expr> EnvironmentVariables { get; init; } = new();

    public FunctionExpr? EnclosingClosureProvidingFunction { get; init; }

    public override IEnumerable<Expr> ChildExpressions
        => Arguments;
}

public class LiteralExpr(Token value, Scope scope)
    : Expr(value.Position, value.EndPosition, scope)
{
    public Token Value { get; } = value;

    public RuntimeObject? RuntimeValue { get; init; }

    public override IEnumerable<Expr> ChildExpressions
        => Array.Empty<Expr>();
}

public class StringInterpolationExpr : Expr
{
    public List<Expr> Parts { get; }

    public bool IsTextArgument { get; }

    public override IEnumerable<Expr> ChildExpressions
        => Parts;

    public StringInterpolationExpr(List<Expr> parts, TextPos pos, Scope scope)
        : base(pos, parts.LastOrDefault()?.EndPosition ?? pos, scope)
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

public class ClosureExpr(
    CallExpr function,
    List<Token> parameters,
    BlockExpr body,
    Scope scope)
    : Expr(body.StartPosition, body.EndPosition, scope)
{
    public CallExpr Function { get; } = function;

    public List<Token> Parameters { get; } = parameters;

    public BlockExpr Body { get; set; } = body;

    public HashSet<string> CapturedVariables { get; init; } = [];

    public override IEnumerable<Expr> ChildExpressions
        => [Function, Body];
}

public class TryExpr(
    BlockExpr body,
    IList<CatchExpr> catchExpressions,
    Scope scope)
    : Expr(
        body.StartPosition,
        catchExpressions.LastOrDefault()?.EndPosition ?? body.EndPosition,
        scope
    )
{
    public BlockExpr Body { get; } = body;

    public IList<CatchExpr> CatchExpressions { get; } = catchExpressions;

    public override IEnumerable<Expr> ChildExpressions
        => CatchExpressions.Prepend<Expr>(Body);
}

public class CatchExpr(Token? identifier, TypeExpr? type, BlockExpr body, Scope scope)
    : Expr(body.StartPosition, body.EndPosition, scope)
{
    public Token? Identifier { get; } = identifier;

    public TypeExpr? Type { get; } = type;

    public BlockExpr Body { get; } = body;

    public override IEnumerable<Expr> ChildExpressions
        => [Body];
}