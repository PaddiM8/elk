using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeString : IRuntimeValue
{
    public RuntimeType DataType => RuntimeType.String;

    public string Value { get; }

    public RuntimeString(string value)
    {
        Value = value;
    }

    public IRuntimeValue Cast(RuntimeType type)
    {
        return type switch
        {
            RuntimeType.String => this,
            RuntimeType.Number => new RuntimeNumber(double.Parse(Value)),
            RuntimeType.Boolean => RuntimeBoolean.From(Value.Length != 0),
            _ => throw new NotImplementedException(),
        };
    }

    public IRuntimeValue Operation(TokenKind kind)
        => kind switch
        {
            TokenKind.Minus => Cast(RuntimeType.Number).Operation(kind),
            TokenKind.Exclamation => RuntimeBoolean.From(Value.Length == 0),
            _ => throw new NotImplementedException(),
        };

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        if (kind is TokenKind.Minus or TokenKind.Star or TokenKind.Slash)
        {
            return Cast(RuntimeType.Number).Operation(kind, other);
        }

        var otherString = (RuntimeString)other.Cast(DataType);
        return kind switch
        {
            TokenKind.Plus => new RuntimeString(Value + otherString.Value),
            TokenKind.Greater => RuntimeBoolean.From(string.Compare(Value, otherString.Value) > 0),
            TokenKind.GreaterEquals => RuntimeBoolean.From(string.Compare(Value, otherString.Value) >= 0),
            TokenKind.Less => RuntimeBoolean.From(string.Compare(Value, otherString.Value) < 0),
            TokenKind.LessEquals => RuntimeBoolean.From(string.Compare(Value, otherString.Value) <= 0),
            TokenKind.EqualsEquals => RuntimeBoolean.From(Value == otherString.Value),
            TokenKind.NotEquals => RuntimeBoolean.From(Value != otherString.Value),
            _ => throw new NotImplementedException(),
        };
    }

    public override string ToString()
        => Value;
}