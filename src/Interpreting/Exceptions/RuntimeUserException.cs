using Elk.Std.DataTypes;

namespace Elk.Interpreting.Exceptions;

class RuntimeUserException(RuntimeObject value)
    : RuntimeException(
        value is RuntimeNil
            ? ""
            : value.ToString()
    )
{
    public RuntimeObject Value { get; } = value;
}