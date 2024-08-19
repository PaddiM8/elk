using System.Collections.Generic;
using Elk.Scoping;
using Elk.Std.DataTypes;

namespace Elk.Vm;

class VirtualMachineContext
{
    public IndexableStack<RuntimeObject> Stack { get; } = new();

    public Dictionary<VariableSymbol, RuntimeObject> Variables { get; } = new();

    public Stack<ExceptionFrame> ExceptionStack { get; } = new();
}