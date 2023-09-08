#region

using System.Reflection;

#endregion

namespace Elk.Interpreting.Exceptions;

class RuntimeIterationException : RuntimeException
{
    public RuntimeIterationException(MemberInfo type)
        : base($"Cannot iterate over type {ExceptionFormatting.Type(type)}")
    {
    }
}