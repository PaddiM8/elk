using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeBoolean : IRuntimeValue
{
    public RuntimeType DataType => RuntimeType.Boolean;

    private bool _value;

    public RuntimeBoolean(bool value)
    {
        _value = value;
    }

    public IRuntimeValue Cast(RuntimeType type)
    {
        return type switch
        {
            RuntimeType.Boolean => this,
            RuntimeType.String => new RuntimeString(_value.ToString()),
            _ => throw new NotImplementedException(),
        };
    }

    public IRuntimeValue Operation(TokenKind kind)
        => new RuntimeBoolean(!_value);

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherNumber = (RuntimeBoolean)other.Cast(DataType);
        var newValue = kind switch
        {
            TokenKind.EqualsEquals => _value == otherNumber._value,
            TokenKind.NotEquals => _value != otherNumber._value,
            TokenKind.And => _value && otherNumber._value,
            TokenKind.Or => _value || otherNumber._value,
            _ => throw new NotImplementedException(),
        };

        return new RuntimeBoolean(newValue);
    }

    public override string ToString()
        => _value.ToString();
}