using System;

namespace Shel;

class RuntimeUnableToIndexException : RuntimeException
{
    public RuntimeUnableToIndexException(Type type)
        : base($"Cannot index values of type {type.Name[7..]}")
    {
    }
}