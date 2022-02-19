using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeString : IRuntimeValue
{
    public RuntimeType DataType => RuntimeType.String;

    private string _value;

    public RuntimeString(string value)
    {
        _value = value;
    }

    public IRuntimeValue Cast(RuntimeType type)
    {
        return type switch
        {
            RuntimeType.String => this,
            RuntimeType.Number => new RuntimeNumber(double.Parse(_value)),
            RuntimeType.Boolean => new RuntimeBoolean(_value.Length != 0),
            _ => throw new NotImplementedException(),
        };
    }

    public IRuntimeValue Operation(TokenKind kind)
        => kind switch
        {
            TokenKind.Minus => Cast(RuntimeType.Number).Operation(kind),
            TokenKind.Exclamation => new RuntimeBoolean(_value.Length == 0),
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
            TokenKind.Plus => new RuntimeString(_value + otherString._value),
            TokenKind.Greater => new RuntimeBoolean(string.Compare(_value, otherString._value) > 0),
            TokenKind.GreaterEquals => new RuntimeBoolean(string.Compare(_value, otherString._value) >= 0),
            TokenKind.Less => new RuntimeBoolean(string.Compare(_value, otherString._value) < 0),
            TokenKind.LessEquals => new RuntimeBoolean(string.Compare(_value, otherString._value) <= 0),
            TokenKind.EqualsEquals => new RuntimeBoolean(_value == otherString._value),
            TokenKind.NotEquals => new RuntimeBoolean(_value != otherString._value),
            _ => throw new NotImplementedException(),
        };
    }

    public override string ToString()
        => _value;
}