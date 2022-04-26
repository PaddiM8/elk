namespace Elk.Interpreting.Exceptions;

class RuntimeUnableToHashException<T> : RuntimeException
{
    public RuntimeUnableToHashException()
        : base($"Cannot hash values of type {typeof(T).Name[7..]}")
    {
    }
}