using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Elk.Std.DataTypes;

namespace Elk.Std.Bindings;

public record StdFunctionParameter(
    Type Type,
    string Name,
    bool IsNullable = false,
    bool IsClosure = false
);

public record StdFunction(
    string? ModuleName,
    string Name,
    string? Documentation,
    int MinArgumentCount,
    int MaxArgumentCount,
    bool HasClosure,
    int? VariadicStart,
    bool ConsumesPipe,
    bool StartsPipeManually,
    ImmutableArray<StdFunctionParameter> Parameters,
    Func<List<object?>, RuntimeObject> Invoke)
{
    public int? ClosureParameterCount
    {
        get
        {
            if (Parameters.Length == 0)
                return null;

            var lastParameterType = Parameters.Last().Type.ToString();
            if (lastParameterType.StartsWith("System.Func`"))
                return lastParameterType[lastParameterType.IndexOf('`') + 1] - '0' - 1;

            if (lastParameterType.StartsWith("System.Action`"))
                return lastParameterType[lastParameterType.IndexOf('`') + 1] - '0';

            return null;
        }
    }
}