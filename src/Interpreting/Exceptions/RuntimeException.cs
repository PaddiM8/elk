using System;

namespace Elk;

class RuntimeException : Exception
{
    public RuntimeException(string message)
        : base(message)
    {
    }
}