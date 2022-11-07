#region

using System;
using Elk.Parsing;

#endregion

namespace Elk.Std.DataTypes;

public abstract class RuntimeObject : IComparable<RuntimeObject>
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

    public int CompareTo(RuntimeObject? other)
    {
        if (other == null)
            return 1;

        if (((RuntimeBoolean)Operation(OperationKind.Less, other)).Value)
            return -1;

        if (((RuntimeBoolean)Operation(OperationKind.EqualsEquals, other)).Value)
            return 0;

        return 1;
    }
}