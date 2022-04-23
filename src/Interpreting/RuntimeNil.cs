using System;
using Elk.Lexing;

namespace Elk.Interpreting;

class RuntimeNil : IRuntimeValue
{
    public static RuntimeNil Value = new();

    public IRuntimeValue As(Type toType)
        => toType switch
        {
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.False,
            _
                => throw new RuntimeCastException<RuntimeNil>(toType),
        };

    public IRuntimeValue Operation(TokenKind kind)
        => kind == TokenKind.Exclamation
            ? RuntimeBoolean.True
            : this;

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
        => kind switch
        {
            TokenKind.Equals => RuntimeBoolean.From(other is RuntimeNil),
            TokenKind.NotEquals => RuntimeBoolean.From(other is not RuntimeNil),
            _ => this,
        };

    public override int GetHashCode()
        => throw new RuntimeUnableToHashException<RuntimeNil>();

    public override string ToString()
        => "nil";
}