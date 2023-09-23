using Elk.Std.DataTypes;

namespace Elk.Interpreting.Exceptions;

class RuntimeUserException : RuntimeException
{
    public RuntimeObject Value { get; }

    public RuntimeUserException(RuntimeObject value)
        : base("")
    {
        Value = value;
    }
}