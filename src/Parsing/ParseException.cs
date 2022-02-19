using System;

namespace Shel.Parsing;

class ParseException : Exception
{
    public ParseException(string message)
        : base(message)
    {
    }
}