using System;

namespace Elk.ReadLine.Render;
    
internal interface IRenderer
{
    int CursorLeft { get; }
    
    int CursorTop { get; }
    
    int BufferWidth { get; }
    
    int BufferHeight { get; }

    int InputStart { get; }

    int Caret { get; set; }

    bool CaretVisible { get; set; }

    string Text { get; set; }
    
    string? HintText { get; }
    
    bool IsEndOfLine { get; }
    
    void OnHighlight(Func<string, string>? callback);
    
    void OnHint(Func<string, string?>? callback);

    void CaretUp();
    
    void CaretDown();

    void Clear();
    
    void ClearLineLeft(int? fromIndex = null);

    void ClearLineRight(int? fromIndex = null);

    void RemoveLeft(int count);

    void RemoveRight(int count);

    void Insert(string input, bool includeHint);

    void RenderText(bool includeHint = false);

    void WriteLinesOutside(string value, int rowCount, int lastLineLength);
    
}
