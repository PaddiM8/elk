namespace Elk.Exceptions;

class RuntimeWrongNumberOfArgumentsException : RuntimeException
{
    public RuntimeWrongNumberOfArgumentsException(int expected, int got, bool variadic = false)
        : base(
            variadic
                ? $"Wrong number of arguments. Expected {expected} or more but got {got}"
                : $"Wrong numbers of arguments. Expected {expected} but got {got}"
        )
    {
    }
}