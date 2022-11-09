#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

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
            .Where(x => x.Namespace?.StartsWith("Elk.Std.DataTypes") ?? false)
            .Where(x => x.GetCustomAttribute<ElkTypeAttribute>() != null);
        foreach (var type in types)
        {
            if (type.BaseType?.GetCustomAttribute<ElkTypeAttribute>() != null)
                continue;

            string typeName = type.GetCustomAttribute<ElkTypeAttribute>()!.Name;
            _runtimeTypes.Add(typeName, type);
        }

        _runtimeTypes.Add("Iterable", typeof(IEnumerable<RuntimeObject>));

        return _runtimeTypes;
    }
}