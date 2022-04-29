using System.Reflection;

namespace Elk.Interpreting.Exceptions;

class RuntimeIterationException : RuntimeException
{
    public RuntimeIterationException(MemberInfo type)
        : base($"Cannot iterate over type {type.Name[7..]}")
    {
    }
}