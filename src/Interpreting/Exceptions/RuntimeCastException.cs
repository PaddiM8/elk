using System;

namespace Shel.Interpreting;

class RuntimeCastException<T, K> : RuntimeException
{
    public RuntimeCastException()
        : base($"Cannot cast from {typeof(T).Name[7..]} to {typeof(K).Name[7..]}")
    {
    }
}