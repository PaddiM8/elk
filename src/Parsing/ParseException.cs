using System;
using Shel.Lexing;

namespace Shel.Parsing;

internal class ParseException : Exception
{
    public TextPos Position { get; }

    public ParseException(TextPos position, string message)
        : base(message)
    {
        Position = position;
    }
}