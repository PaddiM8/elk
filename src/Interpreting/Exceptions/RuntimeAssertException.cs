using System.Reflection;

namespace Elk.Interpreting.Exceptions;

class RuntimeAssertException : RuntimeException
{
    public RuntimeAssertException()
        : base("Assertion failed")
    {
    }
}