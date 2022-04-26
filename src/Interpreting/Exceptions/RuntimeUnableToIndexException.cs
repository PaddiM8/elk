using System;

namespace Elk.Interpreting.Exceptions;

class RuntimeUnableToIndexException : RuntimeException
{
    public RuntimeUnableToIndexException(Type type)
        : base($"Cannot index values of type {type.Name[7..]}")
    {
    }
}