namespace Elk.ReadLine;
public record Completion(string CompletionText, string DisplayText, string? Description = null)
{
    public bool HasTrailingSpace { get; init; }

    public Completion(string completionText)
        : this(completionText, completionText)
    {
    }
}