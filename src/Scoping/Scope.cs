#region

using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Parsing;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Scoping;

public abstract class Scope
{
    public ModuleScope ModuleScope { get; protected init; }

    public Scope? Parent { get; set; }

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

    public VariableSymbol AddVariable(string name, RuntimeObject value)
    {
        var symbol = new VariableSymbol(name, value);
        if (!_variables.TryAdd(name, symbol))
        {
            UpdateVariable(name, value);

            return _variables[name];
        }

        return symbol;
    }

    public void ClearVariables()
    {
        foreach (var symbol in _variables.Values)
            symbol.Value = RuntimeNil.Value;
    }

    public bool HasVariable(string name)
        => _variables.ContainsKey(name) || (Parent?.HasVariable(name) ?? false);

    public bool HasDeclarationOfVariable(string name)
        => _variables.ContainsKey(name);

    public VariableSymbol? FindVariable(string name)
    {
        _variables.TryGetValue(name, out var result);

        return result ?? Parent?.FindVariable(name);
    }

    public bool UpdateVariable(string name, RuntimeObject value)
    {
        if (_variables.TryGetValue(name, out var variable))
        {
            variable.Value = value;
        }
        else
        {
            if (Parent == null)
                return false;

            Parent.UpdateVariable(name, value);
        }

        return true;
    }

    public IEnumerable<ISymbol> Query(string name, bool includePrivate)
    {
        IEnumerable<ISymbol> modules = ModuleScope.Modules
            .Concat(
                includePrivate
                    ? ModuleScope.ImportedModules
                    : Array.Empty<ModuleScope>()
            )
            .Where(x => includePrivate || x.AccessLevel == AccessLevel.Public)
            .Where(x => x.Name?.Contains(name) is true);
        IEnumerable<ISymbol> structs = ModuleScope
            .Structs
            .Concat(
                includePrivate
                    ? ModuleScope.ImportedStructs
                    : Array.Empty<StructSymbol>()
            )
            .Where(x => includePrivate || x.Expr?.AccessLevel == AccessLevel.Public)
            .Where(x => x.Name.Contains(name));
        IEnumerable<ISymbol> functions = ModuleScope
            .Functions
            .Concat(
                includePrivate
                    ? ModuleScope.ImportedFunctions
                    : Array.Empty<FunctionSymbol>()
            )
            .Where(x => includePrivate || x.Expr.AccessLevel == AccessLevel.Public)
            .Where(x => x.Expr.Identifier.Value.Contains(name));
        var locals = includePrivate
            ? QueryLocals(name)
            : Array.Empty<ISymbol>();

        return modules
            .Concat(structs)
            .Concat(functions)
            .Concat(locals);
    }

    private IEnumerable<ISymbol> QueryLocals(string name)
    {
        return _variables
            .Where(x => x.Key.Contains(name))
            .Select(x => x.Value)
            .Concat(
                Parent?.QueryLocals(name)
                    ?? Array.Empty<ISymbol>()
            );
    }
}