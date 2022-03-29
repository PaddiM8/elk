using System;
using System.Collections.Generic;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeBoolean : IRuntimeValue
{
    public static RuntimeBoolean True => new(true);
    public static RuntimeBoolean False => new(false);

    public bool Value { get; }

    private RuntimeBoolean(bool value)
    {
        Value = value;
    }

    public T As<T>()
        where T : IRuntimeValue
    {
        IRuntimeValue converted = typeof(T) switch
        {
            var type when type == typeof(RuntimeBoolean)
                => this,
            var type when type == typeof(RuntimeString)
                => new RuntimeString(Value.ToString()),
            _
                => throw new RuntimeCastException<RuntimeBoolean, T>(),
        };

        return (T)converted;
    }

    public static RuntimeBoolean From(bool value)
        => value ? RuntimeBoolean.True : RuntimeBoolean.False;

    public IRuntimeValue Operation(TokenKind kind)
        => new RuntimeBoolean(!Value);

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
    {
        var otherBoolean = (RuntimeBoolean)other;
        var newValue = kind switch
        {
            TokenKind.EqualsEquals => Value == otherBoolean.Value,
            TokenKind.NotEquals => Value != otherBoolean.Value,
            TokenKind.And => Value && otherBoolean.Value,
            TokenKind.Or => Value || otherBoolean.Value,
            _ => throw new NotImplementedException(),
        };

        return new RuntimeBoolean(newValue);
    }

    public override string ToString()
        => Value.ToString();
}