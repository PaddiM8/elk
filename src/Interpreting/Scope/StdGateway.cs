#region

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Interpreting.Exceptions;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Interpreting.Scope;

static class StdGateway
{
    private static readonly Dictionary<string, MethodInfo> _methods = new();
    private static readonly Dictionary<string, List<string>> _modules = new();

    public static bool Contains(string name, string? moduleName = null)
    {
        if (!_methods.Any())
            Initialize();

        return _methods.ContainsKey(
            moduleName == null ? name : $"{moduleName}::{name}"
        );
    }

    public static bool ContainsModule(string name)
        => _modules.ContainsKey(name);

    public static IRuntimeValue Call(string name, string? module, List<object?> arguments, ShellEnvironment shellEnvironment)
    {
        if (!_methods.Any())
            Initialize();

        string key = module == null
            ? name
            : $"{module}::{name}";
        _methods.TryGetValue(key, out MethodInfo? methodInfo);

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
                    throw new RuntimeWrongNumberOfArgumentsException(parameters.Length, arguments.Count);
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

    public static List<string>? FindModuleFunctions(string moduleName)
    {
        _modules.TryGetValue(moduleName, out var functionNames);

        return functionNames;
    }

    private static void Initialize()
    {
        var stdClasses = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(x => x.Namespace == "Elk.Std");
        foreach (var stdType in stdClasses)
        {
            string? moduleName = stdType.GetCustomAttribute<ElkModuleAttribute>()?.Name;

            var methods = stdType.GetMethods();
            var functionNames = new List<string>();
            foreach (var method in methods)
            {
                var functionAttribute = method.GetCustomAttribute<ElkFunctionAttribute>();
                if (functionAttribute == null)
                    continue;

                string key = functionAttribute.Reachability == Reachability.Module
                    ? $"{moduleName}::{functionAttribute.Name}"
                    : functionAttribute.Name;

                _methods.Add(key, method);
                functionNames.Add(functionAttribute.Name);
            }

            if (moduleName != null)
                _modules.Add(moduleName, functionNames);
        }
    }
}