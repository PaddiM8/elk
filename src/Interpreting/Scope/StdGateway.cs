#region

using System;
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
    {
        if (!_modules.Any())
            Initialize();

        return _modules.ContainsKey(name);
    }

    public static MethodInfo? GetFunction(string name, string? module)
    {
        if (!_methods.Any())
            Initialize();

        string key = module == null
            ? name
            : $"{module}::{name}";
        _methods.TryGetValue(key, out var methodInfo);

        return methodInfo;
    }

    public static RuntimeObject Call(MethodInfo methodInfo,
        List<object?> arguments,
        ShellEnvironment shellEnvironment,
        Func<IEnumerable<RuntimeObject>, RuntimeObject> closure)
    {
        var parameters = methodInfo.GetParameters();
        int? variadicStart = null;
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

            if (parameter.GetCustomAttribute<ElkVariadicAttribute>() == null)
            {
                arguments[i] = ResolveArgument(arguments[i], parameter.ParameterType);
            }
            else
            {
                variadicStart = i;
                break;
            }
        }

        if (variadicStart != null)
        {
            var variadicArgument = arguments
                .Skip(variadicStart.Value)
                .Where(argument => argument != null)
                .Cast<RuntimeObject>()
                .ToList();
            arguments.RemoveRange(variadicStart.Value, variadicArgument.Count);
            arguments.Add(variadicArgument);
        }

        if (parameters.LastOrDefault()?.ParameterType == typeof(ShellEnvironment))
            arguments.Add(shellEnvironment);

        if (ResolveClosure(closure, parameters.LastOrDefault()?.ParameterType, out object? closureArgument))
            arguments.Add(closureArgument);

        try
        {
            return methodInfo.Invoke(null, arguments.ToArray()) as RuntimeObject
                ?? RuntimeNil.Value;
        }
        catch(TargetInvocationException e)
        {
            throw e.InnerException ?? new RuntimeException("Unknown error");
        }
    }

    private static object? ResolveArgument(object? argument, Type parameterType)
    {
        if (argument != null &&
            parameterType != typeof(RuntimeObject) &&
            argument is not ShellEnvironment and not Func<RuntimeObject, RuntimeObject>)
        {
            if (parameterType == typeof(IEnumerable<RuntimeObject>))
            {
                if (argument is not IEnumerable<RuntimeObject>)
                    throw new RuntimeCastException(argument.GetType(), "iterable");
            }
            else
            {
                return ((RuntimeObject)argument).As(parameterType);
            }
        }

        return argument;
    }

    private static bool ResolveClosure(
        Func<IEnumerable<RuntimeObject>, RuntimeObject> closure,
        Type? type,
        out object? closureArgument)
    {
        if (type == typeof(Func<RuntimeObject >))
        {
            closureArgument = new Func<RuntimeObject>(()
                => closure(Array.Empty<RuntimeObject>()));
            return true;
        }

        if (type == typeof(Func<RuntimeObject, RuntimeObject>))
        {
            closureArgument = new Func<RuntimeObject, RuntimeObject>(a
                => closure(new[] { a }));
            return true;
        }

        if (type == typeof(Func<RuntimeObject, RuntimeObject, RuntimeObject>))
        {
            closureArgument = new Func<RuntimeObject, RuntimeObject, RuntimeObject>((a, b)
                => closure(new[] { a, b }));
            return true;
        }

        if (type == typeof(Func<IEnumerable<RuntimeObject>, RuntimeObject>))
        {
            closureArgument = new Func<IEnumerable<RuntimeObject>, RuntimeObject>(closure);
            return true;
        }

        closureArgument = null;

        return false;
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