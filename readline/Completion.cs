using System;

namespace Elk.ReadLine;
public record Completion(string CompletionText, string DisplayText, string? Description = null)
{
    public string DisplayText
    {
        get => _displayText;
        init => _displayText = value.Replace(Environment.NewLine, " ");
    }

    public bool HasTrailingSpace { get; init; }

    private readonly string _displayText = DisplayText.Replace(Environment.NewLine, " ");

    public Completion(string completionText)
        : this(completionText, completionText)
    {
    }
}