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

    public static IRuntimeValue Call(MethodInfo methodInfo,
        List<object?> arguments,
        ShellEnvironment shellEnvironment,
        Func<IEnumerable<IRuntimeValue>, IRuntimeValue> closure)
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
                .Cast<IRuntimeValue>()
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
            return methodInfo.Invoke(null, arguments.ToArray()) as IRuntimeValue
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
            parameterType != typeof(IRuntimeValue) &&
            argument is not ShellEnvironment and not Func<IRuntimeValue, IRuntimeValue>)
        {
            if (parameterType == typeof(IEnumerable<IRuntimeValue>))
            {
                if (argument is not IEnumerable<IRuntimeValue>)
                    throw new RuntimeCastException(argument.GetType(), "iterable");
            }
            else
            {
                return ((IRuntimeValue)argument).As(parameterType);
            }
        }

        return argument;
    }

    private static bool ResolveClosure(
        Func<IEnumerable<IRuntimeValue>, IRuntimeValue> closure,
        Type? type,
        out object? closureArgument)
    {
        if (type == typeof(Func<IRuntimeValue >))
        {
            closureArgument = new Func<IRuntimeValue>(()
                => closure(Array.Empty<IRuntimeValue>()));
            return true;
        }

        if (type == typeof(Func<IRuntimeValue, IRuntimeValue>))
        {
            closureArgument = new Func<IRuntimeValue, IRuntimeValue>(a
                => closure(new[] { a }));
            return true;
        }

        if (type == typeof(Func<IRuntimeValue, IRuntimeValue, IRuntimeValue>))
        {
            closureArgument = new Func<IRuntimeValue, IRuntimeValue, IRuntimeValue>((a, b)
                => closure(new[] { a, b }));
            return true;
        }

        if (type == typeof(Func<IEnumerable<IRuntimeValue>, IRuntimeValue>))
        {
            closureArgument = new Func<IEnumerable<IRuntimeValue>, IRuntimeValue>(closure);
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