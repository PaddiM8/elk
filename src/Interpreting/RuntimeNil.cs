using System;
using Shel.Lexing;

namespace Shel.Interpreting;

class RuntimeNil : IRuntimeValue
{
    public static RuntimeNil Value = new();

    public RuntimeType DataType => RuntimeType.Nil;

    public IRuntimeValue Cast(RuntimeType type)
        => type switch
        {
            RuntimeType.Boolean => RuntimeBoolean.False,
            _ => this,
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

    public override string ToString()
        => "";
}