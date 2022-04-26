using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Attributes;
using Elk.Interpreting.Exceptions;

namespace Elk.Interpreting.Scope;

static class StdGateway
{
    private static readonly Dictionary<string, MethodInfo> _methods = new();

    public static bool Contains(string name)
    {
        if (!_methods.Any())
            Initialize();

        return _methods.ContainsKey(name);
    }

    public static IRuntimeValue Call(string name, List<object?> arguments, ShellEnvironment shellEnvironment)
    {
        if (!_methods.Any())
            Initialize();

        _methods.TryGetValue(name, out MethodInfo? methodInfo);

        if (methodInfo == null)
            return RuntimeNil.Value;

        var parameters = methodInfo.GetParameters();
        if (parameters.LastOrDefault()?.ParameterType == typeof(ShellEnvironment))
            arguments.Add(shellEnvironment);

        foreach (var (parameter, i) in parameters.WithIndex())
        {
            if (i >= arguments.Count)
            {
                if (parameter.HasDefaultValue)
                {
                    arguments.Add(null);
                }
                else
                {
                    throw new RuntimeWrongNumberOfArgumentsException(parameters.Count(), arguments.Count);
                }
            }

            var parameterType = parameter.ParameterType;
            if (arguments[i] != null &&
                parameterType != typeof(IRuntimeValue) &&
                arguments[i] is not ShellEnvironment)
            {
                arguments[i] = ((IRuntimeValue)arguments[i]!).As(parameter.ParameterType);
            }
        }

        try
        {
            return methodInfo.Invoke(null, arguments.ToArray()) as IRuntimeValue
                ?? RuntimeNil.Value;
        }
        catch(TargetInvocationException e)
        {
            throw e.InnerException ?? new RuntimeException("Unknown error");
        }
    }

    private static void Initialize()
    {
        var stdTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.Namespace?.StartsWith("Elk.Std") ?? false);
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