using System;

namespace Shel;

class RuntimeIterationException : RuntimeException
{
    public RuntimeIterationException(Type type)
        : base($"Cannot iterate over type {type.Name[7..]}")
    {
    }
}