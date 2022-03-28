using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeBoolean : IRuntimeValue
{
    public static RuntimeBoolean True => new(true);
    public static RuntimeBoolean False => new(false);

    public RuntimeType DataType => RuntimeType.Boolean;

    public bool Value { get; }

    private RuntimeBoolean(bool value)
    {
        Value = value;
    }

    public IRuntimeValue Cast(RuntimeType type)
    {
        return type switch
        {
            RuntimeType.Boolean => this,
            RuntimeType.String => new RuntimeString(Value.ToString()),
            _ => throw new RuntimeCastException(DataType, type),
        };
    }

    public static RuntimeBoolean From(bool value)
        => value ? RuntimeBoolean.True : RuntimeBoolean.False;

    public IRuntimeValue Operation(TokenKind kind)
        => new RuntimeBoolean(!Value);

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherNumber = (RuntimeBoolean)other.Cast(DataType);
        var newValue = kind switch
        {
            TokenKind.EqualsEquals => Value == otherNumber.Value,
            TokenKind.NotEquals => Value != otherNumber.Value,
            TokenKind.And => Value && otherNumber.Value,
            TokenKind.Or => Value || otherNumber.Value,
            _ => throw new NotImplementedException(),
        };

        return new RuntimeBoolean(newValue);
    }

    public override string ToString()
        => Value.ToString();
}