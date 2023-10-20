namespace Elk.ReadLine;
public record Completion(string CompletionText, string DisplayText, string? Description = null)
{
    public Completion(string completionText)
        : this(completionText, completionText)
    {
    }
}