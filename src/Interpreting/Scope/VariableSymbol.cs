using Elk.Std.DataTypes;

namespace Elk.Interpreting.Scope;

public class VariableSymbol(string name, RuntimeObject value) : ISymbol
{
    public string Name { get; } = name;

    public RuntimeObject Value { get; set; } = value;

    public bool IsCaptured { get; set; }
}