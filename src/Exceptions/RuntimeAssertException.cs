using Elk.Std.DataTypes;

namespace Elk.Exceptions;

class RuntimeAssertException : RuntimeException
{
    public RuntimeAssertException()
        : base("Assertion failed")
    {
    }

    public RuntimeAssertException(RuntimeObject got, RuntimeObject expected)
        : base($"Assertion failed. Got: {got} but expected: {expected}")
    {
    }
}