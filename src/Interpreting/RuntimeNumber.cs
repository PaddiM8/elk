using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeNumber : IRuntimeValue
{
    public RuntimeType DataType => RuntimeType.Number;

    public double Value { get; }

    public RuntimeNumber(double value)
    {
        Value = value;
    }

    public IRuntimeValue Cast(RuntimeType type)
    {
        return type switch
        {
            RuntimeType.Number => this,
            RuntimeType.String => new RuntimeString(Value.ToString()),
            RuntimeType.Boolean => new RuntimeBoolean(Value != 0),
            _ => throw new NotImplementedException(),
        };
    }

    public IRuntimeValue Operation(TokenKind kind)
        => kind switch
        {
            TokenKind.Minus => new RuntimeNumber(-Value),
            TokenKind.Exclamation => new RuntimeBoolean(Value == 0),
            _ => throw new NotImplementedException(),
        };

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherNumber = (RuntimeNumber)other.Cast(DataType);
        return kind switch
        {
            TokenKind.Plus => new RuntimeNumber(Value + otherNumber.Value),
            TokenKind.Minus => new RuntimeNumber(Value - otherNumber.Value),
            TokenKind.Star => new RuntimeNumber(Value * otherNumber.Value),
            TokenKind.Slash => new RuntimeNumber(Value / otherNumber.Value),
            TokenKind.Greater => new RuntimeBoolean(Value > otherNumber.Value),
            TokenKind.GreaterEquals => new RuntimeBoolean(Value >= otherNumber.Value),
            TokenKind.Less => new RuntimeBoolean(Value < otherNumber.Value),
            TokenKind.LessEquals => new RuntimeBoolean(Value <= otherNumber.Value),
            TokenKind.EqualsEquals => new RuntimeBoolean(Value == otherNumber.Value),
            TokenKind.NotEquals => new RuntimeBoolean(Value != otherNumber.Value),
            _ => throw new NotImplementedException(),
        };
    }

    public override string ToString()
        => Value.ToString();
}