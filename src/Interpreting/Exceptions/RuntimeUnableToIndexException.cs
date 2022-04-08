using System;

namespace Shel;

class RuntimeUnableToIndexException : Exception
{
    public RuntimeUnableToIndexException(Type type)
        : base(type.Name[7..])
    {
    }
}