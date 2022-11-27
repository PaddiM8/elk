using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    int MinArgumentCount,
    int MaxArgumentCount,
    bool HasClosure,
    int? VariadicStart,
    ImmutableArray<StdFunctionParameter> Parameters,
    Func<List<object?>, RuntimeObject> Invoke);