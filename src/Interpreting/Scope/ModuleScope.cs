using System.Collections.Generic;
using Elk.Parsing;

namespace Elk.Interpreting.Scope;

record FunctionSymbol(FunctionExpr Expr, bool IsImported);
record Alias(string Name, LiteralExpr Value);

class ModuleScope : Scope
{
    private readonly Dictionary<string, FunctionSymbol> _functions = new();

    private readonly Dictionary<string, Alias> _aliases = new();

    public ModuleScope()
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
        var symbol = new FunctionSymbol(function, false);
        if (!_functions.TryAdd(function.Identifier.Value, symbol))
        {
            _functions[function.Identifier.Value] = symbol;
        }
    }

    public bool ContainsFunction(string name)
        => _functions.ContainsKey(name);
    
    public Alias? FindAlias(string name)
    {
        _aliases.TryGetValue(name, out var value);

        return value;
    }

    public FunctionExpr? FindFunction(string name)
    {
        _functions.TryGetValue(name, out var result);

        return result?.Expr;
    }

    public void ImportFunction(FunctionExpr function)
    {
        var symbol = new FunctionSymbol(function, true);
        if (!_functions.TryAdd(function.Identifier.Value, symbol))
        {
            _functions[function.Identifier.Value] = symbol;
        }
    }

    public void RemoveAlias(string name)
    {
        _aliases.Remove(name);
    }
}