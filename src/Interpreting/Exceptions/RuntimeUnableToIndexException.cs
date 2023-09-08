#region

using System.Reflection;

#endregion

namespace Elk.Interpreting.Exceptions;

class RuntimeUnableToIndexException : RuntimeException
{
    public RuntimeUnableToIndexException(MemberInfo type)
        : base($"Cannot index values of type {ExceptionFormatting.Type(type)}")
    {
    }
}