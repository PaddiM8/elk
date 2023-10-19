using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;

namespace Elk.DocGen;

public class SymbolReader
{
    private readonly Dictionary<string, FunctionDocumentation> _docs;

    public SymbolReader(string xmlPath)
    {
        using var docsReader = new XmlDocumentationReader(xmlPath);
        _docs = docsReader.Read();
    }

    public StdInfo Read()
    {
        var stdClasses = AppDomain.CurrentDomain
            .GetAssemblies()
            .First(x => x.GetName().Name == "Elk")
            .GetTypes()
            .Where(x => x.Namespace == "Elk.Std");
        var modules = new List<ModuleInfo>();
        var globalFunctions = new List<FunctionInfo>();
        foreach (var classInfo in stdClasses)
        {
            var functions = new List<FunctionInfo>();
            foreach (var methodInfo in classInfo.GetMethods())
            {
                var attribute = methodInfo.GetCustomAttribute<ElkFunctionAttribute>();
                if (attribute == null)
                    continue;

                var fullName = $"{methodInfo.DeclaringType}.{methodInfo.Name}";
                _docs.TryGetValue(fullName, out var functionDocs);

                var parameters = new List<ParameterInfo>();
                ClosureInfo? closure = null;
                foreach (var (parameter, i) in methodInfo.GetParameters().WithIndex())
                {
                    // Need to do a string comparison here since ShellEnvironment
                    // is internal.
                    var typeName = parameter.ParameterType.Name;
                    if (typeName == "ShellEnvironment")
                        continue;

                    if (typeName.StartsWith("Func`"))
                    {
                        closure = new ClosureInfo(typeName.Last() - '0' - 1);
                        continue;
                    }

                    var parameterDocs = functionDocs?.Parameters.ElementAtOrDefault(i);
                    var description = parameterDocs?.descrption ?? "";

                    ValueInfo valueInfo;
                    if (parameterDocs?.types != null)
                        valueInfo = new ValueInfo(parameterDocs.Value.types, description);
                    else if (parameter.ParameterType == typeof(IEnumerable<RuntimeObject>))
                        valueInfo = new ValueInfo("Iterable", description);
                    else if (parameter.ParameterType == typeof(IIndexable<RuntimeObject>))
                        valueInfo = new ValueInfo("Indexable", description);
                    else
                        valueInfo = new ValueInfo(parameter.ParameterType, description);

                    var isVariadic = parameter.GetCustomAttribute<ElkVariadicAttribute>() != null;
                    var parameterInfo = new ParameterInfo(
                        parameter.Name!,
                        valueInfo,
                        parameter.HasDefaultValue,
                        isVariadic
                    );

                    parameters.Add(parameterInfo);
                }

                var returnValue = new ValueInfo(
                    methodInfo.ReturnType,
                    functionDocs?.Returns ?? ""
                );

                var functionList = attribute.Reachability == Reachability.Everywhere
                    ? globalFunctions
                    : functions;
                var function = new FunctionInfo(attribute.Name, parameters, returnValue)
                {
                    Example = functionDocs?.Example,
                    Summary = functionDocs?.Summary,
                    Errors = functionDocs?.Errors ?? new(),
                    Closure = closure,
                };
                functionList.Add(function);
            }

            var moduleName = classInfo.GetCustomAttribute<ElkModuleAttribute>()?.Name;
            if (moduleName != null)
                modules.Add(new ModuleInfo(moduleName, classInfo.Name, functions));
        }

        return new StdInfo(modules, globalFunctions);
    }
}