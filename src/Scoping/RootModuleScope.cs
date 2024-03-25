using System.Collections.Generic;
using System.IO;
using Elk.Parsing;

namespace Elk.Scoping;

public class RootModuleScope : ModuleScope
{
    private readonly Dictionary<string, ModuleScope> _allModules = new();

    public RootModuleScope(string? filePath, Ast? ast)
        : base(
            AccessLevel.Public,
            Path.GetFileNameWithoutExtension(filePath),
            null,
            filePath,
            ast ?? new Ast([])
        )
    {
        if (FilePath != null)
            _allModules[FilePath] = this;
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