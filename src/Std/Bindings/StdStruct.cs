using System.Collections.Immutable;

namespace Elk.Std.Bindings;

public record StdStruct(
    string? ModuleName,
    string Name,
    int MinArgumentCount,
    int MaxArgumentCount,
    int? VariadicStart,
    ImmutableArray<StdFunctionParameter> Parameters);