using Elk.Std.DataTypes;

namespace Elk.Interpreting.Scope;

class VariableSymbol
{
    public RuntimeObject Value { get; set; }

    public VariableSymbol(RuntimeObject value)
    {
        Value = value;
    }
}