using System;
using Shel.Lexing;

namespace Shel.Interpreting;

interface IRuntimeValue
{
    public T As<T>()
        where T : IRuntimeValue;

    public IRuntimeValue Operation(TokenKind kind);

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other);
}