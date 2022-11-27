namespace Elk.Interpreting.Exceptions;

public class RuntimeInvalidOperationException : RuntimeException
{
    public RuntimeInvalidOperationException(string operation, string context)
        : base($"Cannot perform operation '{operation}' on {context}")
    {
    }
}