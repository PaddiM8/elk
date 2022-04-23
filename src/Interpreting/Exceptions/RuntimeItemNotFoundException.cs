using System;

namespace Elk;

class RuntimeItemNotFoundException : RuntimeException
{
    public RuntimeItemNotFoundException(string item)
        : base($"The item '{item}' was not found")
    {
    }
}