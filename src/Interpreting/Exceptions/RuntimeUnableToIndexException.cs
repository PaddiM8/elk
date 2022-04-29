using System.Reflection;

namespace Elk.Interpreting.Exceptions;

class RuntimeUnableToIndexException : RuntimeException
{
    public RuntimeUnableToIndexException(MemberInfo type)
        : base($"Cannot index values of type {type.Name[7..]}")
    {
    }
}