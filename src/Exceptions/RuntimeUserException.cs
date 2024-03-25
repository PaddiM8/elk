using Elk.Std.DataTypes;

namespace Elk.Exceptions;

class RuntimeUserException(RuntimeError value)
    : RuntimeException(
        value.Value is RuntimeNil
            ? ""
            : value.ToString() ?? ""
    )
{
    public RuntimeError Value { get; } = value;
}