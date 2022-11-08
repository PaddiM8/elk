#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

#endregion

namespace Elk.Interpreting;

enum ClosureParameterCount
{
    Zero,
    One,
    Two,
    Variadic,
}

record StdFunction(
    string Name,
    MethodInfo MethodInfo,
    List<ParameterInfo> Parameters,
    int MinArgumentCount,
    int MaxArgumentCount,
    bool InjectShellEnvironment,
    int? VariadicStart,
    ClosureParameterCount? ClosureParameterCount);

static class StdGateway
{
    private static readonly Dictionary<string, StdFunction> _methods = new();
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

    public static StdFunction? GetFunction(string name, string? module)
    {
        if (!_methods.Any())
            Initialize();

        string key = module == null
            ? name
            : $"{module}::{name}";
        _methods.TryGetValue(key, out var methodInfo);

        return methodInfo;
    }

    public static RuntimeObject Call(StdFunction stdFunction,
        List<RuntimeObject> arguments,
        ShellEnvironment shellEnvironment,
        TextPos position,
        Func<IEnumerable<RuntimeObject>, RuntimeObject> closure)
    {
        int extraArgumentCount = 0;
        if (stdFunction.InjectShellEnvironment)
            extraArgumentCount++;
        if (stdFunction.ClosureParameterCount.HasValue)
            extraArgumentCount++;

        var invokeArguments = new List<object?>(arguments.Count + extraArgumentCount);

        int nonVariadicArgumentCount = stdFunction.VariadicStart ?? stdFunction.Parameters.Count;
        var relevantParameters = stdFunction
            .Parameters
            .GetRange(0, nonVariadicArgumentCount);
        foreach (var (argument, parameter) in arguments.ZipLongest(relevantParameters))
        {
            if (argument != null && parameter != null)
                invokeArguments.Add(ResolveArgument(argument, parameter.ParameterType));
            else if (argument == null && parameter is { HasDefaultValue: true })
                invokeArguments.Add(null);
        }

        if (stdFunction.VariadicStart.HasValue)
        {
            var variadicArgument = arguments
                .Skip(stdFunction.VariadicStart.Value)
                .ToList();
            invokeArguments.Add(variadicArgument);
        }

        if (stdFunction.InjectShellEnvironment)
            invokeArguments.Add(shellEnvironment);

        if (ResolveClosure(closure, stdFunction.ClosureParameterCount, out object? closureArgument))
            invokeArguments.Add(closureArgument!);

        try
        {
            return stdFunction.MethodInfo.Invoke(null, invokeArguments.ToArray()) as RuntimeObject
                   ?? RuntimeNil.Value;
        }
        catch (TargetInvocationException e)
        {
            if (e.InnerException is RuntimeStdException stdException)
                return new RuntimeError(stdException.Message, position);
            if (e.InnerException is RuntimeException runtimeException)
                throw runtimeException;

            throw new RuntimeException("An unknown error occured while calling a function in the standard library");
        }
    }

    private static object ResolveArgument(RuntimeObject argument, Type parameterType)
    {
        if (parameterType == typeof(RuntimeObject))
            return argument;

        if (parameterType == typeof(IEnumerable<RuntimeObject>))
        {
            return argument is IEnumerable<RuntimeObject>
               ? argument
               : throw new RuntimeCastException(argument.GetType(), "Iterable");
        }

        return argument.As(parameterType);
    }

    private static bool ResolveClosure(
        Func<IEnumerable<RuntimeObject>, RuntimeObject> closure,
        ClosureParameterCount? parameterCount,
        out object? closureArgument)
    {
        if (!parameterCount.HasValue)
        {
            closureArgument = null;

            return false;
        }

        if (parameterCount == ClosureParameterCount.Zero)
        {
            closureArgument = new Func<RuntimeObject>(()
                => closure(Array.Empty<RuntimeObject>()));
            return true;
        }

        if (parameterCount == ClosureParameterCount.One)
        {
            closureArgument = new Func<RuntimeObject, RuntimeObject>(a
                => closure(new[] { a }));
            return true;
        }

        if (parameterCount == ClosureParameterCount.Two)
        {
            closureArgument = new Func<RuntimeObject, RuntimeObject, RuntimeObject>((a, b)
                => closure(new[] { a, b }));
            return true;
        }

        closureArgument = new Func<IEnumerable<RuntimeObject>, RuntimeObject>(closure);

        return true;
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

                _methods.Add(key, InitializeFunction(key, method));
                functionNames.Add(functionAttribute.Name);
            }

            if (moduleName != null)
                _modules.Add(moduleName, functionNames);
        }
    }

    private static StdFunction InitializeFunction(string name, MethodInfo method)
    {
        var parameters = new List<ParameterInfo>();
        int minArgumentCount = 0;
        int maxArgumentCount = 0;
        bool injectShellEnvironment = false;
        int? variadicStart = null;
        ClosureParameterCount? closureParameterCount = null;
        foreach (var (parameter, i) in method.GetParameters().WithIndex())
        {
            if (typeof(RuntimeObject).IsAssignableFrom(parameter.ParameterType) ||
                parameter.ParameterType == typeof(IEnumerable<RuntimeObject>))
            {
                if (parameter.HasDefaultValue && minArgumentCount == 0)
                    minArgumentCount = parameters.Count;

                if (parameter.GetCustomAttribute<ElkVariadicAttribute>() != null)
                    variadicStart = i;

                maxArgumentCount++;
                parameters.Add(parameter);
            }
            else if (parameter.ParameterType == typeof(ShellEnvironment))
            {
                injectShellEnvironment = true;
            }
            else if (parameter.ParameterType.Name.StartsWith("Func`"))
            {
                var genericTypeArguments = parameter.ParameterType.GenericTypeArguments;
                int typeArgumentCount = genericTypeArguments.Length;
                if (typeArgumentCount == 2 &&
                    genericTypeArguments[0] == typeof(IEnumerable<RuntimeObject>))
                {
                    closureParameterCount = ClosureParameterCount.Variadic;
                    continue;
                }

                closureParameterCount = typeArgumentCount switch
                {
                    1 => ClosureParameterCount.Zero,
                    2 => ClosureParameterCount.One,
                    3 => ClosureParameterCount.Two,
                    _ => ClosureParameterCount.Variadic,
                };
            }
        }

        if (minArgumentCount == 0)
            minArgumentCount = parameters.Count;

        if (variadicStart.HasValue)
            maxArgumentCount = int.MaxValue;

        return new StdFunction(
            name,
            method,
            parameters,
            minArgumentCount,
            maxArgumentCount,
            injectShellEnvironment,
            variadicStart,
            closureParameterCount
        );
    }
}