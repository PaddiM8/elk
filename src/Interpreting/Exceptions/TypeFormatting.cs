using System;
using System.Reflection;

namespace Elk.Interpreting.Exceptions;

public static class TypeFormatting
{
    public static string Format(MemberInfo type)
        => FromTypeString(type.Name);

    public static string Format(Type type)
        => FromTypeString(type.Name);

    private static string FromTypeString(string typeString)
    {
        return typeString.StartsWith("Runtime")
            ? typeString["Runtime".Length..]
            : typeString;
    }
}