#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Std.Attributes;

#endregion

namespace Elk.Interpreting;

static class LanguageInfo
{
    public static Dictionary<string, Type> RuntimeTypes
        => _runtimeTypes ?? LoadRuntimeTypes();

    private static Dictionary<string, Type>? _runtimeTypes;

    private static Dictionary<string, Type> LoadRuntimeTypes()
    {
        _runtimeTypes = new();

        var types = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.Namespace?.StartsWith("Elk.Interpreting") ?? false)
            .Where(x => x.GetCustomAttribute<ElkTypeAttribute>() != null);
        foreach (var type in types)
        {
            string typeName = type.GetCustomAttribute<ElkTypeAttribute>()!.Name;
            _runtimeTypes.Add(typeName, type);
        }

        return _runtimeTypes;
    }
}