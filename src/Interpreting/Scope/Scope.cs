using System.Collections.Generic;

namespace Elk.Interpreting.Scope;

abstract class Scope
{
    public GlobalScope GlobalScope { get; }

    public Scope? Parent { get; }

    private readonly Dictionary<string, IRuntimeValue> _variables = new();

    protected Scope(Scope? parent)
    {
        GlobalScope = parent switch
        {
            GlobalScope scope => scope,
            LocalScope scope => scope.GlobalScope,
            _ => (GlobalScope)this,
        };
        Parent = parent;
    }

    public void AddVariable(string name, IRuntimeValue value)
    {
        if (!_variables.TryAdd(name, value))
        {
            UpdateVariable(name, value);
        }
    }

    public void Clear()
    {
        _variables.Clear();
    }

    public bool ContainsVariable(string name)
        => _variables.ContainsKey(name) || (Parent?.ContainsVariable(name) ?? false);

    public IRuntimeValue? FindVariable(string name)
    {
        _variables.TryGetValue(name, out var result);

        return result ?? Parent?.FindVariable(name);
    }

    public bool UpdateVariable(string name, IRuntimeValue value)
    {
        if (_variables.ContainsKey(name))
        {
            _variables[name] = value;
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