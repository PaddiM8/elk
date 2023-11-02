﻿using System;
using System.Runtime.InteropServices;
using Elk.ReadLine.Render;

namespace Elk.ReadLine;

public class ReadLinePrompt
{

    public IHistoryHandler? HistoryHandler { private get; set; }

    public IAutoCompleteHandler? AutoCompletionHandler { private get; set; }

    public IHighlightHandler? HighlightHandler { private get; set; }

    public IHintHandler? HintHandler { private get; set; }

    public IEnterHandler? EnterHandler { private get; set; }

    public ISearchHandler? SearchHandler { private get; set; }

    public char[]? WordSeparators { get; set; }

    private KeyHandler? _keyHandler;
    private readonly ShortcutBag _shortcuts = new();
    private readonly object _resizeLock = new();

    public string Read(string prompt = "", string @default = "")
    {
        Console.Write(prompt);
        var enterPressed = false;
        var renderer = new Renderer();
        if (!OperatingSystem.IsWindows())
        {
            PosixSignalRegistration.Create(
                PosixSignal.SIGWINCH,
                _ =>
                {
                    lock (_resizeLock)
                        renderer.Render();
                }
            );
        }

        _keyHandler = new KeyHandler(renderer, _shortcuts)
        {
            HistoryHandler = HistoryHandler,
            AutoCompleteHandler = AutoCompletionHandler,
            HighlightHandler = HighlightHandler,
            HintHandler = HintHandler,
            EnterHandler = EnterHandler,
            SearchHandler = SearchHandler,
            OnEnter = () => enterPressed = true,
        };

        if (WordSeparators != null)
            _keyHandler.WordSeparators = WordSeparators;

        while (!enterPressed)
        {
            var (firstKey, remaining) = KeyReader.Read();
            _keyHandler.Handle(firstKey, remaining);
        }

        var text = _keyHandler.Text;
        if (string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(@default))
        {
            text = @default;
        }

        return text;
    }

    public void RegisterShortcut(KeyPress keyPress, Action<KeyHandler> action)
    {
        _shortcuts.Add(keyPress, action);
    }
}
