using Elk.Std.DataTypes;

namespace Elk.Interpreting.Scope;

public class VariableSymbol(RuntimeObject value)
{
    public RuntimeObject Value { get; set; } = value;
}