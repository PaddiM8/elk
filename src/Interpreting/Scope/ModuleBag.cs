using System.Collections.Generic;

namespace Elk.Interpreting.Scope;

class ModuleBag
{
    private readonly Dictionary<string, ModuleScope> _modules = new();

    public bool TryAdd(string absolutePath, ModuleScope scope)
        => _modules.TryAdd(absolutePath, scope);

    public bool Contains(string absolutePath)
        => _modules.ContainsKey(absolutePath);

    public ModuleScope? Find(string absolutePath)
    {
        _modules.TryGetValue(absolutePath, out var result);

        return result;
    }
}