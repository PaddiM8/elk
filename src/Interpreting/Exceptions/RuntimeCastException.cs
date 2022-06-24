#region

using System.Reflection;

#endregion

namespace Elk.Interpreting.Exceptions;

class RuntimeCastException<T> : RuntimeException
{
    public RuntimeCastException(MemberInfo toType)
        : base($"Cannot cast from {typeof(T).Name[7..]} to {toType.Name[7..]}")
    {
    }
}