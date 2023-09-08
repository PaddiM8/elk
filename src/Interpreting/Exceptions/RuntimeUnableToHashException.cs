namespace Elk.Interpreting.Exceptions;

class RuntimeUnableToHashException<T> : RuntimeException
{
    public RuntimeUnableToHashException()
        : base($"Cannot hash values of type {TypeFormatting.Format(typeof(T))}")
    {
    }
}