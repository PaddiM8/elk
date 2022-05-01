using System.Collections.Generic;
using Elk.Parsing;

namespace Elk.Interpreting.Scope;

record Alias(string Name, LiteralExpr Value);

class GlobalScope : Scope
{
    private readonly Dictionary<string, FunctionExpr> _functions = new();

    private readonly Dictionary<string, Alias> _aliases = new();

    private readonly HashSet<string> _includes = new();

    public GlobalScope()
        : base(null)
    {
    }

    public void AddAlias(string name, LiteralExpr expansion)
    {
        var parts = expansion.Value.Value.Split(' ', 2);
        var argument = expansion.Value with { Value = parts[1] };

        _aliases.Add(
            name,
            new Alias(parts[0], new LiteralExpr(argument))
        );
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

    public Alias? FindAlias(string name)
    {
        _aliases.TryGetValue(name, out var value);

        return value;
    }

    public FunctionExpr? FindFunction(string name)
    {
        _functions.TryGetValue(name, out var result);

        return result;
    }
}