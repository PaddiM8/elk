using System;
using System.Collections.Generic;

namespace Elk.Std.DataTypes.Serialization.CommandLine;

public class CliArgument
{
    public required string Identifier { get; init; }

    public string? Description { get; init; }

    public bool IsRequired { get; init; }

    public CliValueKind ValueKind { get; init; }

    public bool IsVariadic { get; init; }

    public Func<CliResult, IEnumerable<string>>? CompletionHandler { get; init; }
}