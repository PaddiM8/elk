using System;
using System.Runtime.InteropServices;
using Elk.ReadLine.Render;
using Elk.ReadLine.Render.Formatting;

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
    private readonly object _rendererLock = new();
    private Renderer? _activeRenderer;

    public ReadLinePrompt()
    {
        if (OperatingSystem.IsWindows())
            return;
 
        PosixSignalRegistration.Create(PosixSignal.SIGWINCH, HandleResize);
    }

    private void HandleResize(PosixSignalContext context)
    {
        if (_activeRenderer == null)
            return;

        lock (_rendererLock)
        {
            var promptPlaceholder = new string(
                ' ',
                Math.Max(2, _activeRenderer.PromptStartLeft) - 2
            );
            _activeRenderer.WriteRaw(Ansi.MoveToColumn(0) + promptPlaceholder + "❯ ");
            _activeRenderer.Render();
        }
    }

    public string Read(string? prompt = null, string? defaultInput = null)
    {
        if (Console.CursorLeft != 0)
            Console.WriteLine();

        var enterPressed = false;
        var renderer = new Renderer(prompt)
        {
            Text = defaultInput ?? ""
        };
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

        lock (_rendererLock)
            _activeRenderer = renderer;

        while (!enterPressed)
        {
            var (firstKey, remaining) = KeyReader.Read();
            _keyHandler.Handle(firstKey, remaining);
        }

        lock (_rendererLock)
            _activeRenderer = null;

        return _keyHandler.Text;
    }

    public void RegisterShortcut(KeyPress keyPress, Action<KeyHandler> action)
    {
        _shortcuts.Add(keyPress, action);
    }
}
