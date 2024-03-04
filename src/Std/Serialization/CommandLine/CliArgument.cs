using System;
using System.Collections.Generic;
using Elk.ReadLine;

namespace Elk.Std.Serialization.CommandLine;

public class CliArgument
{
    public string? Identifier { get; init; }

    public string? Description { get; init; }

    public bool IsRequired { get; init; }

    public CliValueKind ValueKind { get; init; }

    public bool IsVariadic { get; init; }

    public bool AllowCustomCompletionHints { get; init; }

    public Func<string, CliResult, IEnumerable<Completion>>? CompletionHandler { get; init; }
}