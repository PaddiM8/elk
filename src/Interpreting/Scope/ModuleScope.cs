#region

using System.Collections.Generic;
using System.Linq;
using Elk.Parsing;

#endregion

namespace Elk.Interpreting.Scope;

record FunctionSymbol(FunctionExpr Expr, bool IsImported);
record Alias(string Name, LiteralExpr Value);

class ModuleScope : Scope
{
    public IEnumerable<FunctionExpr> Functions
        => _functions.Values.Select(x => x.Expr);

    private readonly Dictionary<string, FunctionSymbol> _functions = new();

    private readonly Dictionary<string, Alias> _aliases = new();

    private readonly Dictionary<string, string> _importedStdFunctions = new();

    public ModuleScope()
        : base(null)
    {
    }

    public void AddAlias(string name, LiteralExpr expansion)
    {
        var parts = expansion.Value.Value.Split(' ', 2);
        var argument = expansion.Value with { Value = parts[1] };
        var alias = new Alias(parts[0], new LiteralExpr(argument));

        if (!_aliases.TryAdd(name, alias))
        {
            _aliases[name] = alias;
        }
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

    public string? FindImportedStdFunctionModule(string functionName)
    {
        _importedStdFunctions.TryGetValue(functionName, out var result);

        return result;
    }

    public void ImportFunction(FunctionExpr function)
    {
        var symbol = new FunctionSymbol(function, true);
        if (!_functions.TryAdd(function.Identifier.Value, symbol))
        {
            _functions[function.Identifier.Value] = symbol;
        }
    }

    public void ImportStdFunction(string name, string module)
    {
        if (!_importedStdFunctions.TryAdd(name, module))
        {
            _importedStdFunctions[name] = module;
        }
    }

    public void RemoveAlias(string name)
    {
        _aliases.Remove(name);
    }
}