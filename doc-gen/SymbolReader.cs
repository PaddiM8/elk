using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Elk.Std.Attributes;

namespace Elk.DocGen;

public class SymbolReader
{
    private Dictionary<string, FunctionDocumentation> _docs;

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

                string fullName = $"{methodInfo.DeclaringType}.{methodInfo.Name}";
                _docs.TryGetValue(fullName, out var functionDocs);

                var parameters = new List<ParameterInfo>();
                foreach (var (parameter, i) in methodInfo.GetParameters().WithIndex())
                {
                    if (parameter.ParameterType.Name == "ShellEnvironment")
                        continue;

                    var parameterDocs = functionDocs?.Parameters.ElementAtOrDefault(i);
                    string description = parameterDocs?.descrption ?? "";
                    var valueInfo = parameterDocs?.types == null
                        ? new ValueInfo(parameter.ParameterType, description)
                        : new ValueInfo(parameterDocs.Value.types, description);
                    var parameterInfo = new ParameterInfo(parameter.Name!, valueInfo, parameter.HasDefaultValue);

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