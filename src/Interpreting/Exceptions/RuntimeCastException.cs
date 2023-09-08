#region

using System.Reflection;

#endregion

namespace Elk.Interpreting.Exceptions;

class RuntimeCastException<T> : RuntimeException
{
    public RuntimeCastException(MemberInfo toType, string? message = null)
        : base(
            $"Cannot cast from {ExceptionFormatting.Type(typeof(T))} to " +
            $"{ExceptionFormatting.Type(toType)}" +
            $"{ExceptionFormatting.Message(message)}"
        )
    {
    }
}

class RuntimeCastException : RuntimeException
{
    public RuntimeCastException(MemberInfo fromType, string toType, string? message = null)
        : base(
            $"Cannot cast from {ExceptionFormatting.Type(fromType)} to " +
            $"{toType}{ExceptionFormatting.Message(message)}"
        )
    {
    }
}