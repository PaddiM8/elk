using System.Collections.Generic;
using System.IO;

namespace Elk.Interpreting.Scope;

class RootModuleScope : ModuleScope
{
    private readonly Dictionary<string, ModuleScope> _allModules = new();

    public RootModuleScope(string? filePath)
        : base(Path.GetFileNameWithoutExtension(filePath), null, filePath)
    {
        if (filePath != null)
            _allModules[filePath] = this;
    }

    public void RegisterModule(string filePath, ModuleScope module)
    {
        _allModules.TryAdd(filePath, module);
    }

    public ModuleScope? FindRegisteredModule(string filePath)
    {
        _allModules.TryGetValue(filePath, out var module);

        return module;
    }
}