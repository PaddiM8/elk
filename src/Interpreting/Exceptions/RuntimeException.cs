using System;

namespace Shel;

class RuntimeException : Exception
{
    public RuntimeException(string message)
        : base(message)
    {
    }
}