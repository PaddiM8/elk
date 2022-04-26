using System;

namespace Elk.Interpreting.Exceptions;

class RuntimeException : Exception
{
    public RuntimeException(string message)
        : base(message)
    {
    }
}