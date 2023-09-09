namespace Elk.ReadLine;

public class EnterHandlerResponse
{
    public bool WasHandled { get; }

    public string? NewPromptText { get; }

    public int? NewCaretPosition { get; }

    public EnterHandlerResponse(bool wasHandled, string? newPromptText, int? newCaretPosition)
    {
        WasHandled = wasHandled;
        NewPromptText = newPromptText;
        NewCaretPosition = newCaretPosition;
    }
}