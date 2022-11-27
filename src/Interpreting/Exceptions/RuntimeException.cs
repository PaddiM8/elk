#region

using System;
using Elk.Lexing;

#endregion

namespace Elk.Interpreting.Exceptions;

public class RuntimeException : Exception
{
    public TextPos? Position { get; }

    public RuntimeException(string message)
        : base(message)
    {
    }

    public RuntimeException(string message, TextPos? position)
        : base(message)
    {
        Position = position;
    }
}