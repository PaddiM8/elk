namespace Elk.Exceptions;

class RuntimeUnableToHashException<T> : RuntimeException
{
    public RuntimeUnableToHashException()
        : base($"Cannot hash values of type {ExceptionFormatting.Type(typeof(T))}")
    {
    }
}