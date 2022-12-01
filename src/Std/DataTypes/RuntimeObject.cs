#region

using System;
using Elk.Interpreting.Exceptions;
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

    public virtual RuntimeObject Operation(OperationKind kind)
        => throw InvalidOperation(kind);

    public virtual RuntimeObject Operation(OperationKind kind, RuntimeObject other)
        => throw InvalidOperation(kind);

    public virtual string ToDisplayString()
        => ToString()!;

    public int CompareTo(RuntimeObject? other)
    {
        if (other == null)
            return 1;

        // TODO: Optimise this by avoiding the allocation for the boolean result
        if (((RuntimeBoolean)Operation(OperationKind.Less, other)).IsTrue)
            return -1;

        if (((RuntimeBoolean)Operation(OperationKind.EqualsEquals, other)).IsTrue)
            return 0;

        return 1;
    }

    protected RuntimeInvalidOperationException InvalidOperation(OperationKind kind)
        => new(kind.ToString(), GetType().Name[7..]);
}