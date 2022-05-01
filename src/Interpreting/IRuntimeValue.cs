using System;
using Elk.Lexing;
using Elk.Parsing;

namespace Elk.Interpreting;

interface IRuntimeValue
{
    public IRuntimeValue As(Type toType);

    public T As<T>()
        where T : IRuntimeValue
    {
        return (T)As(typeof(T));
    }

    public IRuntimeValue Operation(OperationKind kind);

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other);
}