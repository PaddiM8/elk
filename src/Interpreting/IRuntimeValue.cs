using System;
using Elk.Lexing;

namespace Elk.Interpreting;

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