using Elk.Std.DataTypes;

namespace Elk.Interpreting.Exceptions;

class RuntimeUserException(RuntimeError value)
    : RuntimeException(value.ToString() ?? "")
{
    public RuntimeError Value { get; } = value;
}