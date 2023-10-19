using System;
using System.Collections.Generic;

namespace Elk.ReadLine;

internal class ShortcutBag
{
    private readonly Dictionary<KeyPress, Action<KeyHandler>> _shortcuts = new();

    public void Add(KeyPress key, Action<KeyHandler> action)
    {
        _shortcuts.Add(key, action);
    }

    public bool TryGetValue(KeyPress key, out Action<KeyHandler>? action)
    {
        var success = _shortcuts.TryGetValue(key, out var retrievedAction);
        action = retrievedAction;

        return success;
    }
}