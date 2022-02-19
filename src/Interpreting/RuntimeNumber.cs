using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeNumber : IRuntimeValue
{
    public RuntimeType DataType => RuntimeType.Number;

    private double _value;

    public RuntimeNumber(double value)
    {
        _value = value;
    }

    public IRuntimeValue Cast(RuntimeType type)
    {
        return type switch
        {
            RuntimeType.Number => this,
            RuntimeType.String => new RuntimeString(_value.ToString()),
            RuntimeType.Boolean => new RuntimeBoolean(_value != 0),
            _ => throw new NotImplementedException(),
        };
    }

    public IRuntimeValue Operation(TokenKind kind)
        => kind switch
        {
            TokenKind.Minus => new RuntimeNumber(-_value),
            TokenKind.Exclamation => new RuntimeBoolean(_value == 0),
            _ => throw new NotImplementedException(),
        };

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherNumber = (RuntimeNumber)other.Cast(DataType);
        return kind switch
        {
            TokenKind.Plus => new RuntimeNumber(_value + otherNumber._value),
            TokenKind.Minus => new RuntimeNumber(_value - otherNumber._value),
            TokenKind.Star => new RuntimeNumber(_value * otherNumber._value),
            TokenKind.Slash => new RuntimeNumber(_value / otherNumber._value),
            TokenKind.Greater => new RuntimeBoolean(_value > otherNumber._value),
            TokenKind.GreaterEquals => new RuntimeBoolean(_value >= otherNumber._value),
            TokenKind.Less => new RuntimeBoolean(_value < otherNumber._value),
            TokenKind.LessEquals => new RuntimeBoolean(_value <= otherNumber._value),
            TokenKind.EqualsEquals => new RuntimeBoolean(_value == otherNumber._value),
            TokenKind.NotEquals => new RuntimeBoolean(_value != otherNumber._value),
            _ => throw new NotImplementedException(),
        };
    }

    public override string ToString()
        => _value.ToString();
}