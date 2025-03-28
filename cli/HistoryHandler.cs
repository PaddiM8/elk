using System;
using System.Collections.Generic;
using System.Linq;
using Elk.ReadLine;
using Elk.Cli.Database;
using Nito.Collections;

namespace Elk.Cli;

enum HistoryMode
{
    All,
    WithStart,
}

class HistoryHandler : IHistoryHandler
{
    private readonly int _maxEntries;
    private readonly HistoryRepository _historyRepository;
    private readonly Deque<HistoryEntry> _allEntries;
    private Deque<HistoryEntry> _activeEntries;
    private int _currentIndex;
    private string? _promptText;
    private HistoryMode _historyMode;

    private HistoryHandler(
        int maxEntries,
        HistoryRepository historyRepository,
        IReadOnlyCollection<HistoryEntry> entries)
    {
        _maxEntries = maxEntries;
        _historyRepository = historyRepository;
        _allEntries = new Deque<HistoryEntry>(entries);
        _activeEntries = _allEntries;
        _currentIndex = entries.Count;
    }

    public static HistoryHandler Init(int maxEntries, HistoryRepository historyRepository)
    {
        var entries = historyRepository.GetAll();
        entries.Reverse();

        return new HistoryHandler(maxEntries, historyRepository, entries);
    }

    public void Add(HistoryEntry entry)
    {
        ResetHistoryMode();

        // If the last entry was the same, don't add it again.
        if (_activeEntries.LastOrDefault()?.Content == entry.Content)
        {
            _currentIndex = _allEntries.Count;

            return;
        }

        _allEntries.AddToBack(entry);
        if (_allEntries.Count > _maxEntries)
            _allEntries.RemoveFromFront();

        _historyRepository.Add(entry);
        _currentIndex = _allEntries.Count;
    }

    public string? GetNext(string promptText, int caret, bool wasEdited)
    {
        if (wasEdited && caret != 0)
        {
            var entries = _historyRepository.GetWithStart(promptText);
            entries.Reverse();
            _activeEntries = new Deque<HistoryEntry>(entries);
            _currentIndex = Math.Max(0, _activeEntries.Count - 1);
            _historyMode = HistoryMode.WithStart;
            _promptText = promptText;

            return _activeEntries.LastOrDefault()?.Content;
        }

        if (_historyMode != HistoryMode.All && caret == 0)
            ResetHistoryMode();

        if (_currentIndex == 0)
            return null;

        if (_currentIndex == _activeEntries.Count)
            _promptText = promptText;

        _currentIndex--;

        return _activeEntries[_currentIndex].Content;
    }

    public string? GetPrevious(string promptText, int caret)
    {
        if (_historyMode != HistoryMode.All && caret == 0)
            ResetHistoryMode();

        if (_currentIndex == _activeEntries.Count)
            return null;

        _currentIndex++;

        return _currentIndex == _activeEntries.Count
            ? _promptText
            : _activeEntries[_currentIndex].Content;
    }

    private void ResetHistoryMode()
    {
        _historyMode = HistoryMode.All;
        _activeEntries = _allEntries;
        _currentIndex = _activeEntries.Count;
    }
}
