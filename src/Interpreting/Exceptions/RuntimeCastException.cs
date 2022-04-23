using System;

namespace Elk.Interpreting;

class RuntimeCastException<T> : RuntimeException
{
    public RuntimeCastException(Type toType)
        : base($"Cannot cast from {typeof(T).Name[7..]} to {toType.Name[7..]}")
    {
    }
}