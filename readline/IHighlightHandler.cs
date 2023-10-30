namespace Elk.ReadLine;

public interface IHighlightHandler
{
    public string Highlight(string text, int caret);
}