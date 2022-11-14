#region

using System;
using System.Collections.Generic;
using Elk.Lexing;
using Elk.Parsing;

#endregion

namespace Elk.Interpreting.Scope;

record Alias(string Name, LiteralExpr Value);

class ModuleScope : Scope
{
    public IEnumerable<ModuleScope> Modules
        => _modules.Values;

    public IEnumerable<ModuleScope> ImportedModules
        => _importedModules.Values;

    public IEnumerable<FunctionSymbol> Functions
        => _functions.Values;

    public IEnumerable<FunctionSymbol> ImportedFunctions
        => _importedFunctions.Values;

    public string? Name { get; }

    public string? FilePath { get; }

    public bool IsAnalysed { get; set; }

    public RootModuleScope RootModule { get; }

    private readonly Dictionary<string, ModuleScope> _modules = new();

    private readonly Dictionary<string, ModuleScope> _importedModules = new();

    private readonly Dictionary<string, FunctionSymbol> _functions = new();

    private readonly Dictionary<string, FunctionSymbol> _importedFunctions = new();

    private readonly Dictionary<string, Alias> _aliases = new();

    private readonly Dictionary<string, string> _importedStdFunctions = new();

    public ModuleScope(string? name, Scope? parent, string? filePath)
        : base(parent)
    {
        ModuleScope = this;
        Name = name;
        RootModule = parent?.ModuleScope.RootModule ?? (RootModuleScope)this;
        FilePath = filePath;
    }

    private ModuleScope(string name, RootModuleScope rootModule, string filePath)
        : base(null)
    {
        ModuleScope = this;
        Name = name;
        RootModule = rootModule;
        FilePath = filePath;
    }

    public static ModuleScope CreateAsImported(
        string name,
        RootModuleScope rootModule,
        string filePath)
        => new(name, rootModule, filePath);

    public void AddAlias(string name, LiteralExpr expansion)
    {
        var parts = expansion.Value.Value.Split(' ', 2);
        var argument = expansion.Value with { Value = parts[1] };
        var alias = new Alias(parts[0], new LiteralExpr(argument));

        if (!_aliases.TryAdd(name, alias))
            _aliases[name] = alias;
    }

    public void AddFunction(FunctionExpr function)
    {
        var symbol = new FunctionSymbol(function);
        if (!_functions.TryAdd(function.Identifier.Value, symbol))
            _functions[function.Identifier.Value].Expr = function;
    }

    public void AddModule(string name, ModuleScope module)
    {
        _modules.TryAdd(name, module);
    }

    public bool HasFunction(string name)
        => _functions.ContainsKey(name);

    public Alias? FindAlias(string name)
    {
        _aliases.TryGetValue(name, out var value);

        return value;
    }

    public FunctionSymbol? FindFunction(string name, bool lookInImports)
    {
        if (_functions.TryGetValue(name, out var result))
            return result;

        if (lookInImports && _importedFunctions.TryGetValue(name, out var resultImport))
            return resultImport;

        return result ?? Parent?.ModuleScope.FindFunction(name, lookInImports);
    }

    public ModuleScope? FindModule(IEnumerable<Token> modulePath, bool lookInImports)
    {
        var queue = new Queue<Token>(modulePath);
        var current = this;
        while (queue.Count > 0 && current != null)
        {
            current = current.FindModule(
                queue.Dequeue().Value,
                lookInImports,
                lookInParents: current == this
            );
        }

        return current;
    }

    private ModuleScope? FindModule(
        string moduleName,
        bool lookInImports,
        bool lookInParents)
    {
        if (_modules.TryGetValue(moduleName, out var module))
            return module;

        if (lookInImports && _importedModules.TryGetValue(moduleName, out var moduleImport))
            return moduleImport;

        return lookInParents
            ? Parent?.ModuleScope.FindModule(moduleName, lookInImports, lookInParents)
            : null;
    }

    public string? FindImportedStdFunctionModule(string functionName)
    {
        _importedStdFunctions.TryGetValue(functionName, out var result);

        return result;
    }

    public void ImportFunction(FunctionSymbol symbol)
    {
        var function = symbol.Expr;
        if (!_importedFunctions.TryAdd(function.Identifier.Value, symbol))
            _importedFunctions[function.Identifier.Value] = symbol;
    }

    public void ImportStdFunction(string name, string module)
    {
        if (!_importedStdFunctions.TryAdd(name, module))
            _importedStdFunctions[name] = module;
    }

    public void ImportModule(string name, ModuleScope module)
    {
        if (!_importedModules.TryAdd(name, module))
            _importedModules[name] = module;
    }

    public void RemoveAlias(string name)
    {
        _aliases.Remove(name);
    }
}