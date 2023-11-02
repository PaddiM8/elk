using System;

namespace Elk.ReadLine.Render;

internal interface IRenderer : IRenderable
{
    int CursorLeft { get; }

    int CursorTop { get; }

    int BufferHeight { get; }

    int WindowHeight { get; }

    int InputStart { get; }

    int Caret { get; set; }

    string Text { get; set; }

    string? HintText { get; }

    bool IsEndOfLine { get; }

    void Add(IRenderable renderable);

    void OnHighlight(Func<string, int, string>? callback);

    void OnHint(Func<string, string?>? callback);

    void CaretUp();

    void CaretDown();

    void Clear();

    void ClearLineLeft(int? fromIndex = null);

    void ClearLineRight(int? fromIndex = null);

    void RemoveLeft(int count, bool render = true);

    void RemoveRight(int count, bool render = true);

    void Insert(string input, bool includeHint);

    void RenderText(bool includeHint = false);

    void WriteLinesOutside(string value, int rowCount, int lastLineLength);

    void WriteRaw(params string[] value);

    void WriteRaw(string value);

}
