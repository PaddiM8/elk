using System;

namespace Elk.Cli.Database;

class HistoryEntry
{
    public long Id { get; init; }

    public required string Path { get; init; }

    public required string Content { get; init; }

    public DateTime Time { get; init; }
}