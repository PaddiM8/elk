using System;
using SQLite;

namespace Elk.Cli.Database;

class HistoryEntry
{
    [PrimaryKey, AutoIncrement]
    public long Id { get; init; }
    
    [Indexed]
    public string Path { get; init; } = null!;

    [Indexed]
    public string Content { get; init; } = null!;

    [Indexed]
    public DateTime Time { get; init; }
}