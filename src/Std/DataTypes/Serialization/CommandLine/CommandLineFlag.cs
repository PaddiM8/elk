namespace Elk.Std.DataTypes.Serialization.CommandLine;

public class CommandLineFlag
{
    public required string Identifier { get; init; }

    public string? LongName { get; init; }

    public string? ShortName { get; init; }

    public string? Description { get; init; }

    public string? Format { get; init; }

    public bool ExpectsValue { get; init; }

    public bool IsRequired { get; init; }
}