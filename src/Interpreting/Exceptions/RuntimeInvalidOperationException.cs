using System;

namespace Shel.Interpreting;

class RuntimeInvalidOperationException : RuntimeException
{
    public RuntimeInvalidOperationException(string operation, string context)
        : base($"Cannot perform operation '{operation}' on {context}")
    {
    }
}