namespace Elk.ReadLine;

public class Completion
{
    public string CompletionText { get; }

    public string DisplayText { get; }

    public string? Description { get; }

    public Completion(string completionText, string? displayText = null, string? description = null)
    {
        CompletionText = completionText;
        DisplayText = displayText ?? completionText;
        Description = description;
    }
}