#region

using System.Reflection;

#endregion

namespace Elk.Interpreting.Exceptions;

class RuntimeCastException<T> : RuntimeException
{
    public RuntimeCastException(MemberInfo toType)
        : base($"Cannot cast from {TypeFormatting.Format(typeof(T))} to {TypeFormatting.Format(toType)}")
    {
    }
}

class RuntimeCastException : RuntimeException
{
    public RuntimeCastException(MemberInfo fromType, string toType)
        : base($"Cannot cast from {TypeFormatting.Format(fromType)} to {toType}")
    {
    }
}