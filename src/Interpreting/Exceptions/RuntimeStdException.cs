namespace Elk.Interpreting.Exceptions;

class RuntimeStdException : RuntimeException
{
    public RuntimeStdException(string message)
        : base(message)
    {
    }
}