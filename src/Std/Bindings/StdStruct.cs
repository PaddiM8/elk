using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Elk.Std.DataTypes;

namespace Elk.Std.Bindings;

public record StdStruct(
    string? ModuleName,
    string Name,
    int MinArgumentCount,
    int MaxArgumentCount,
    int? VariadicStart,
    ImmutableArray<StdFunctionParameter> Parameters);