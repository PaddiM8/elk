using System.Collections.Generic;
using Shel.Interpreting;

namespace Shel;

class Scope
{
    public Scope? Parent;

    public Dictionary<string, IRuntimeValue> _variables = new();

    public Scope(Scope? parent)
    {
        Parent = parent;
    }

    public void AddVariable(string name, IRuntimeValue value)
    {
        if (!_variables.TryAdd(name, value))
        {
            UpdateVariable(name, value);
        }
    }

    public bool ContainsVariable(string name)
        => _variables.ContainsKey(name) || (Parent?.ContainsVariable(name) ?? false);

    public IRuntimeValue? FindVariable(string name)
    {
        _variables.TryGetValue(name, out IRuntimeValue? result);

        return result ?? Parent?.FindVariable(name);
    }

    public void UpdateVariable(string name, IRuntimeValue value)
    {
        _variables[name] = value;
    }
}