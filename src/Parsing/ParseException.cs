#region

using System;
using Elk.Lexing;

#endregion

namespace Elk.Parsing;

internal class ParseException : Exception
{
    public TextPos Position { get; }

    public ParseException(TextPos position, string message)
        : base(message)
    {
        Position = position;
    }
}