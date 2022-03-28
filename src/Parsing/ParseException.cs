using System;

namespace Shel.Parsing;

internal class ParseException : Exception
{
    public ParseException(string message)
        : base(message)
    {
    }
}