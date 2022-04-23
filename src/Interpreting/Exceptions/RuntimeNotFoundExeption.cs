using System;

namespace Elk.Interpreting;

class RuntimeNotFoundException : RuntimeException
{
    public RuntimeNotFoundException(string identifier)
        : base($"No such file/function/variable: {identifier}")
    {
    }
}