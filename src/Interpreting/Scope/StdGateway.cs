using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Shel.Interpreting;

static class StdGateway
{
    public static readonly Dictionary<string, MethodInfo> _methods = new();

    public static bool Contains(string name)
    {
        if (!_methods.Any())
            Initialize();

        return _methods.ContainsKey(name);
    }

    public static IRuntimeValue Call(string name, IRuntimeValue[] arguments)
    {
        if (!_methods.Any())
            Initialize();

        _methods.TryGetValue(name, out MethodInfo? methodInfo);

        if (methodInfo == null)
            return RuntimeNil.Value;

        return methodInfo.Invoke(null, (object[])arguments) as IRuntimeValue
            ?? RuntimeNil.Value;
    }

    private static void Initialize()
    {
        var stdTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.Namespace?.StartsWith("Shel.Std") ?? false);
        foreach (Type stdType in stdTypes)
        {
            var methods = stdType.GetMethods();
            foreach (MethodInfo method in methods)
            {
                var attribute = method.GetCustomAttribute<ShellFunctionAttribute>();
                if (attribute == null)
                    continue;
                
                _methods.Add(attribute.Name, method);
            }
        }
    }
}