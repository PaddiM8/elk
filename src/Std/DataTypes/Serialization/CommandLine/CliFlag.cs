using System;
using System.Collections.Generic;

namespace Elk.Std.DataTypes.Serialization.CommandLine;

public class CliFlag
{
    public required string Identifier { get; init; }

    public string? LongName { get; init; }

    public string? ShortName { get; init; }

    public string? Description { get; init; }

    public string? Format { get; init; }

    public bool ExpectsValue { get; init; }

    public CliValueKind ValueKind { get; init; }

    public bool IsRequired { get; init; }

    public Func<CliResult, IEnumerable<string>>? CompletionHandler { get; init; }
}