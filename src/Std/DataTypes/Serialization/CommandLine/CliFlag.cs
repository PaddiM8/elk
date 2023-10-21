using System;
using System.Collections.Generic;
using Elk.ReadLine;

namespace Elk.Std.DataTypes.Serialization.CommandLine;

public class CliFlag
{
    public string? Identifier { get; init; }

    public string? LongName { get; init; }

    public string? ShortName { get; init; }

    public string? Description { get; init; }

    public string? Format { get; init; }

    public CliValueKind ValueKind { get; init; }

    public bool IsRequired { get; init; }

    public Func<string, CliResult, IEnumerable<Completion>>? CompletionHandler { get; init; }
}