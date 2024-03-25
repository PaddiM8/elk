using System;
using System.Reflection;

namespace Elk.Interpreting.Exceptions;

public static class ExceptionFormatting
{
    public static string Message(string? message)
        => message == null ? "" : $". {message}";

    public static string Type(MemberInfo type)
        => FromTypeString(type.Name);

    public static string Type(Type type)
        => FromTypeString(type.Name);

    private static string FromTypeString(string typeString)
    {
        return typeString.StartsWith("Runtime")
            ? typeString["Runtime".Length..]
            : typeString;
    }
}