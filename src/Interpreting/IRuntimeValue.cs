using System;
using Shel.Lexing;

namespace Shel.Interpreting;

enum RuntimeType
{
    Nil,
    Number,
    String,
    Boolean,
}

interface IRuntimeValue
{
    public RuntimeType DataType { get; }

    public IRuntimeValue Cast(RuntimeType type);

    public IRuntimeValue Operation(TokenKind kind);

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other);
}