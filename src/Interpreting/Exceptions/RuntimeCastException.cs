using System;

namespace Shel.Interpreting;

class RuntimeCastException : RuntimeException
{
    public RuntimeCastException(RuntimeType from, RuntimeType to)
        : base($"Cannot cast from {from} to {to}")
    {
    }
}