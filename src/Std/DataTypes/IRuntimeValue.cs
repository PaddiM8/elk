#region

using System;
using Elk.Parsing;

#endregion

namespace Elk.Std.DataTypes;

public interface IRuntimeValue
{
    public IRuntimeValue As(Type toType);

    public T As<T>()
        where T : IRuntimeValue
    {
        return (T)As(typeof(T));
    }

    public IRuntimeValue Operation(OperationKind kind);

    public IRuntimeValue Operation(OperationKind kind, IRuntimeValue other);

    public string ToDisplayString()
        => ToString()!;
}