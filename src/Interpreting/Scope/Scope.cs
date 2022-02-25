using System;
using System.Collections.Generic;
using Shel.Interpreting;

namespace Shel;

abstract class Scope
{
    public GlobalScope GlobalScope { get; }

    public Scope? Parent { get; }

    public Dictionary<string, IRuntimeValue> _variables = new();

    public Scope(Scope? parent)
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