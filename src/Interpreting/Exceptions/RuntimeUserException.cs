namespace Elk.Interpreting.Exceptions;

class RuntimeUserException : RuntimeException
{
    public RuntimeUserException(string message)
        : base($"'{message}'")
    {
    }
}