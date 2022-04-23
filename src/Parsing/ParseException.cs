using System;
using Elk.Lexing;

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