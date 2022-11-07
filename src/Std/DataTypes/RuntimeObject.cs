#region

using System;
using Elk.Parsing;

#endregion

namespace Elk.Std.DataTypes;

public abstract class RuntimeObject
{
    public abstract RuntimeObject As(Type toType);

    public T As<T>()
        where T : RuntimeObject
    {
        return (T)As(typeof(T));
    }

    public abstract RuntimeObject Operation(OperationKind kind);

    public abstract RuntimeObject Operation(OperationKind kind, RuntimeObject other);

    public virtual string ToDisplayString()
        => ToString()!;
}