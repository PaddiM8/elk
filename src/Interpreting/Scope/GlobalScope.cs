using System.Collections.Generic;

namespace Elk;

class GlobalScope : Scope
{
    public Dictionary<string, FunctionExpr> _functions = new();

    public HashSet<string> _includes = new();

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

    public void AddInclude(string absolutePath)
    {
        _includes.Add(absolutePath);
    }

    public bool ContainsFunction(string name)
        => _functions.ContainsKey(name);
    
    public bool ContainsInclude(string absolutePath)
        => _includes.Contains(absolutePath);

    public FunctionExpr? FindFunction(string name)
    {
        _functions.TryGetValue(name, out FunctionExpr? result);

        return result;
    }
}