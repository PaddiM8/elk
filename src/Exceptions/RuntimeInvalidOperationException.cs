using System.Reflection;

namespace Elk.Exceptions;

public class RuntimeInvalidOperationException : RuntimeException
{
    public RuntimeInvalidOperationException(string operation, string context)
        : base($"Cannot perform operation '{operation}' on {context}")
    {
    }

    public RuntimeInvalidOperationException(string operation, MemberInfo type)
        : base($"Cannot perform operation '{operation}' on {ExceptionFormatting.Type(type)}")
    {
    }
}