using System;
using System.Collections.Generic;

namespace Shel;

class GlobalScope : Scope
{
    public Dictionary<string, FunctionExpr> _functions = new();

    public GlobalScope()
        : base(null)
    {
    }

    public void AddFunction(FunctionExpr function)
    {
        if (!_functions.TryAdd(function.Identifier.Value, function))
        {
            _functions[function.Identifier.Value] = function;
        }
    }

    public bool ContainsFunction(string name)
        => _functions.ContainsKey(name);

    public FunctionExpr? FindFunction(string name)
    {
        _functions.TryGetValue(name, out FunctionExpr? result);

        return result;
    }
}