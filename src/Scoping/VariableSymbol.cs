using Elk.Std.DataTypes;

namespace Elk.Scoping;

public class VariableSymbol(string name, RuntimeObject value) : ISymbol
{
    public string Name { get; } = name;

    public RuntimeObject Value { get; set; } = value;

    public bool IsCaptured { get; set; }
}