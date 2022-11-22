using System;
using System.Collections.Generic;
using System.IO;
using SQLite;

namespace Elk.Cli.Database;

class HistoryRepository
{
    private readonly int _maxEntries;
    private readonly SQLiteConnection _db;
    
    public HistoryRepository(int maxEntries)
    {
        _maxEntries = maxEntries;
        
        var path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "elk"
        );
        Directory.CreateDirectory(path);

        _db = new SQLiteConnection(Path.Combine(path, "history.db"));
        _db.CreateTable<HistoryEntry>();
    }
    
    public List<HistoryEntry> GetAll()
    {
        return _db.Table<HistoryEntry>()
            .OrderBy(x => x.Time)
            .Take(250)
            .ToList();
    }

    public List<HistoryEntry> GetWithPath(string path)
    {
        return _db.Table<HistoryEntry>()
            .Where(x => x.Path == path)
            .Take(50)
            .ToList();
    }
    
    public List<HistoryEntry> GetWithStart(string start)
    {
        return _db.Table<HistoryEntry>()
            .Where(x => x.Content.StartsWith(start))
            .Take(50)
            .ToList();
    }

    public void Add(HistoryEntry entry)
    {
        _db.Insert(entry);
        _db.Execute(
            $"""
            DELETE FROM HistoryEntry WHERE Id IN
               (SELECT Id FROM HistoryEntry
                  ORDER BY Time
                  LIMIT CASE
                      WHEN (SELECT MAX(rowid) FROM HistoryEntry) - (SELECT MIN(rowid) FROM HistoryEntry) > {_maxEntries}
                          THEN {Math.Max(2, _maxEntries / 12)}
                          ELSE 0
                      END);
        """
        );
    }
}