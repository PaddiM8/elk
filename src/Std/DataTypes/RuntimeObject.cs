#region

using System;
using Elk.Exceptions;
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

    public virtual int CompareTo(RuntimeObject? other)
    {
        throw new RuntimeInvalidOperationException("comparison", GetType());
    }

    protected RuntimeInvalidOperationException InvalidOperation(OperationKind kind)
        => new(kind.ToString(), GetType().Name[7..]);
}