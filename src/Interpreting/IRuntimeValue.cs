using System;
using Shel.Lexing;

namespace Shel.Interpreting;

interface IRuntimeValue
{
    public IRuntimeValue As(Type toType);

    public T As<T>()
        where T : IRuntimeValue
    {
        return (T)As(typeof(T));
    }

    public IRuntimeValue Operation(TokenKind kind);

    public IRuntimeValue Operation(TokenKind kind, IRuntimeValue other);
}