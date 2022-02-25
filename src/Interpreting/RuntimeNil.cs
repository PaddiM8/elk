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
            RuntimeType.Boolean => new RuntimeBoolean(false),
            _ => this,
        };

    public IRuntimeValue Operation(TokenKind kind)
        => kind == TokenKind.Exclamation
            ? new RuntimeBoolean(true)
            : this;

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other)
        => kind switch
        {
            TokenKind.Equals => new RuntimeBoolean(other is RuntimeNil),
            TokenKind.NotEquals => new RuntimeBoolean(other is not RuntimeNil),
            _ => this,
        };

    public override string ToString()
        => "";
}