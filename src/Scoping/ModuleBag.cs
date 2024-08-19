#region

using System.Collections;
using System.Collections.Generic;

#endregion

namespace Elk.Scoping;

class ModuleBag : IEnumerable<ModuleScope>
{

    public IEnumerator<ModuleScope> GetEnumerator()
        => _modules.Values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

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