#region

using System.Collections.Generic;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Interpreting.Scope;

abstract class Scope
{
    public ModuleScope ModuleScope { get; }

    public Scope? Parent { get; }

    private readonly Dictionary<string, VariableSymbol> _variables = new();

    protected Scope(Scope? parent)
    {
        ModuleScope = parent switch
        {
            ModuleScope scope => scope,
            LocalScope scope => scope.ModuleScope,
            _ => (ModuleScope)this,
        };
        Parent = parent;
    }

    public void AddVariable(string name, IRuntimeValue value)
    {
        if (!_variables.TryAdd(name, new VariableSymbol(value)))
        {
            UpdateVariable(name, value);
        }
    }

    public void Clear()
    {
        foreach (var symbol in _variables.Values)
            symbol.Value = RuntimeNil.Value;
    }

    public bool ContainsVariable(string name)
        => _variables.ContainsKey(name) || (Parent?.ContainsVariable(name) ?? false);

    public VariableSymbol? FindVariable(string name)
    {
        _variables.TryGetValue(name, out var result);

        return result ?? Parent?.FindVariable(name);
    }

    public bool UpdateVariable(string name, IRuntimeValue value)
    {
        if (_variables.ContainsKey(name))
        {
            _variables[name].Value = value;
        }
        else
        {
            if (Parent == null)
                return false;

            Parent.UpdateVariable(name, value);
        }

        return true;
    }
}