using System.Collections.Generic;
using Elk.Parsing;

namespace Elk.Interpreting.Scope;

class GlobalScope : Scope
{
    private readonly Dictionary<string, FunctionExpr> _functions = new();

    private readonly HashSet<string> _includes = new();

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
        _functions.TryGetValue(name, out var result);

        return result;
    }
}