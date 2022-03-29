using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeNil : IRuntimeValue
{
    public static RuntimeNil Value = new();

    public T As<T>()
        where T : IRuntimeValue
    {
        IRuntimeValue converted = typeof(T) switch
        {
            var type when type == typeof(RuntimeBoolean)
                => RuntimeBoolean.False,
            _
                => this,
        };

        return (T)converted;
    }

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

    public override string ToString()
        => "";
}