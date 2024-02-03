#region

using System.Collections.Generic;
using System.IO;
using System.Linq;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Std.Bindings;

#endregion

namespace Elk.Interpreting.Scope;

public record Alias(string Name, IEnumerable<LiteralExpr> Arguments);

public record UnknownSymbol(ModuleScope Module, Token Token);

public class ModuleScope : Scope
{
    public IEnumerable<UnknownSymbol> ImportedUnknowns
        => _importedUnknowns.Values;

    public IEnumerable<ModuleScope> Modules
        => _modules.Values;

    public IEnumerable<ModuleScope> ImportedModules
        => _importedModules.Values;

    public IEnumerable<StructSymbol> Structs
        => _structs.Values;

    public IEnumerable<StructSymbol> ImportedStructs
        => _importedStructs.Values;

    public IEnumerable<FunctionSymbol> Functions
        => _functions.Values;

    public IEnumerable<FunctionSymbol> ImportedFunctions
        => _importedFunctions.Values;

    public AccessLevel AccessLevel { get; }

    public string? Name { get; }

    public string? FilePath { get; }

    public IList<Expr> Ast { get; set; }

    internal AnalysisStatus AnalysisStatus { get; set; }

    public RootModuleScope RootModule { get; }

    private readonly Dictionary<string, UnknownSymbol> _importedUnknowns = new();
    private readonly Dictionary<string, ModuleScope> _modules = new();
    private readonly Dictionary<string, ModuleScope> _importedModules = new();
    private readonly Dictionary<string, StructSymbol> _structs = new();
    private readonly Dictionary<string, StructSymbol> _importedStructs = new();
    private readonly Dictionary<string, FunctionSymbol> _functions = new();
    private readonly Dictionary<string, FunctionSymbol> _importedFunctions = new();
    private readonly Dictionary<string, Alias> _aliases = new();
    private readonly Dictionary<string, string> _importedStdStructs = new();
    private readonly Dictionary<string, string> _importedStdFunctions = new();

    public ModuleScope(
        AccessLevel accessLevel,
        string? name,
        Scope? parent,
        string? filePath,
        IList<Expr> ast)
        : base(parent)
    {
        AccessLevel = accessLevel;
        ModuleScope = this;
        Name = name;
        RootModule = parent?.ModuleScope.RootModule ?? (RootModuleScope)this;
        FilePath = filePath == null
            ? null
            : Path.GetFullPath(filePath);
        Ast = ast;
    }

    private ModuleScope(
        AccessLevel accessLevel,
        string name,
        RootModuleScope rootModule,
        string filePath,
        IList<Expr> ast)
        : base(null)
    {
        AccessLevel = accessLevel;
        ModuleScope = this;
        Name = name;
        RootModule = rootModule;
        FilePath = filePath;
        Ast = ast;
    }

    public static ModuleScope CreateAsImported(
        AccessLevel accessLevel,
        string name,
        RootModuleScope rootModule,
        string filePath,
        IList<Expr> ast)
        => new(accessLevel, name, rootModule, filePath, ast);

    public void AddAlias(string name, IEnumerable<LiteralExpr> arguments)
    {
        var alias = new Alias(arguments.First().Value.Value, arguments.Skip(1));

        if (!_aliases.TryAdd(name, alias))
            _aliases[name] = alias;
    }

    public void AddModule(string name, ModuleScope module)
    {
        _modules.TryAdd(name, module);
    }

    public void AddStruct(StructExpr structExpr)
    {
        var symbol = new StructSymbol(structExpr);
        if (!_structs.TryAdd(structExpr.Identifier.Value, symbol))
            _structs[structExpr.Identifier.Value].Expr = structExpr;
    }

    public void AddFunction(FunctionExpr function)
    {
        var symbol = new FunctionSymbol(function);
        if (!_functions.TryAdd(function.Identifier.Value, symbol))
            _functions[function.Identifier.Value].Expr = function;
    }

    public bool ContainsStruct(string name)
        => _structs.ContainsKey(name);

    public Alias? FindAlias(string name)
    {
        _aliases.TryGetValue(name, out var value);

        return value;
    }

    public ModuleScope? FindModule(IEnumerable<string> modulePath, bool lookInImports)
    {
        var queue = new Queue<string>(modulePath);
        var current = this;
        while (queue.Count > 0 && current != null)
        {
            current = current.FindModule(
                queue.Dequeue(),
                lookInImports,
                lookInParents: current == this
            );
        }

        return current;
    }

    public ModuleScope? FindModule(IEnumerable<Token> modulePath, bool lookInImports)
        => FindModule(modulePath.Select(x => x.Value), lookInImports);

    private ModuleScope? FindModule(
        string moduleName,
        bool lookInImports,
        bool lookInParents)
    {
        if (_modules.TryGetValue(moduleName, out var module))
            return module;

        if (lookInImports && _importedModules.TryGetValue(moduleName, out var moduleImport))
        {
            return moduleImport.AccessLevel != AccessLevel.Public
                ? null
                : moduleImport;
        }

        return lookInParents
            ? Parent?.ModuleScope.FindModule(moduleName, lookInImports, lookInParents)
            : null;
    }

    public string? FindImportedStdStructModule(string structName)
    {
        _importedStdStructs.TryGetValue(structName, out var result);

        return result;
    }

    public string? FindImportedStdFunctionModule(string functionName)
    {
        _importedStdFunctions.TryGetValue(functionName, out var result);

        return result;
    }

    public StructSymbol? FindStruct(string name, bool lookInImports)
    {
        if (_structs.TryGetValue(name, out var result))
            return result;

        if (lookInImports && _importedStructs.TryGetValue(name, out var resultImport))
        {
            return resultImport.Expr?.AccessLevel is not (AccessLevel.Public or null)
                ? null
                : resultImport;
        }

        return result ?? Parent?.ModuleScope.FindStruct(name, lookInImports);
    }

    public FunctionSymbol? FindFunction(string name, bool lookInImports)
    {
        if (_functions.TryGetValue(name, out var result))
            return result;

        if (lookInImports && _importedFunctions.TryGetValue(name, out var resultImport))
        {
            return resultImport.Expr.AccessLevel != AccessLevel.Public
                ? null
                : resultImport;
        }

        return result ?? Parent?.ModuleScope.FindFunction(name, lookInImports);
    }

    public bool ModuleExists(IEnumerable<string> modulePath)
        => FindModule(modulePath, true) != null || StdBindings.HasModule(modulePath);

    public bool StructExists(string name)
        => FindStruct(name, true) != null || StdBindings.HasRuntimeType(name);

    public bool FunctionExists(string name, IEnumerable<string>? modulePath = null)
    {
        var module = modulePath == null
            ? ModuleScope
            : ModuleScope.FindModule(modulePath, true);
        var isLocalFunction = module?.FindFunction(name, true)?.Expr.AnalysisStatus
            is not (null or AnalysisStatus.Failed);

        return isLocalFunction ||
            StdBindings.HasFunction(name, modulePath) ||
            FindImportedStdFunctionModule(name) != null;
    }

    public bool VariableExists(string name)
        => ModuleScope.FindVariable(name) != null;

    public bool AliasExists(string name)
        => ModuleScope.FindAlias(name) != null;


    public void ImportUnknown(ModuleScope importScope, Token token)
    {
        var symbol = new UnknownSymbol(importScope, token);
        if (!_importedUnknowns.TryAdd(token.Value, symbol))
            _importedUnknowns[token.Value] = symbol;
    }

    public void ClearUnknowns()
    {
        _importedUnknowns.Clear();
    }

    public void ImportModule(string name, ModuleScope module)
    {
        if (!_importedModules.TryAdd(name, module))
            _importedModules[name] = module;
    }

    public void ImportStruct(StructSymbol symbol)
    {
        if (!_importedStructs.TryAdd(symbol.Name, symbol))
            _importedStructs[symbol.Name] = symbol;
    }

    public void ImportFunction(FunctionSymbol symbol)
    {
        var function = symbol.Expr;
        if (!_importedFunctions.TryAdd(function.Identifier.Value, symbol))
            _importedFunctions[function.Identifier.Value] = symbol;
    }

    public void ImportStdStruct(string name, string module)
    {
        if (!_importedStdStructs.TryAdd(name, module))
            _importedStdStructs[name] = module;
    }

    public void ImportStdFunction(string name, string module)
    {
        if (!_importedStdFunctions.TryAdd(name, module))
            _importedStdFunctions[name] = module;
    }

    public void RemoveAlias(string name)
    {
        _aliases.Remove(name);
    }
}