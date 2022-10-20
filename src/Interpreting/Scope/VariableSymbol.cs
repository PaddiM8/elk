using Elk.Std.DataTypes;

namespace Elk.Interpreting.Scope;

class VariableSymbol
{
    public IRuntimeValue Value { get; set; }

    public VariableSymbol(IRuntimeValue value)
    {
        Value = value;
    }
}