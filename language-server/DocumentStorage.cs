namespace Elk.LanguageServer;

static class DocumentStorage
{
    private static readonly Dictionary<string, SemanticDocument> _documents = new();

    public static void Add(SemanticDocument document)
    {
        _documents[document.Uri] = document;
    }

    public static void Update(string uri, string text)
    {
        _documents[uri].Text = text;
    }

    public static SemanticDocument Get(string uri)
        => _documents[uri];

    public static void Remove(string uri)
    {
        if (_documents.ContainsKey(uri))
            _documents.Remove(uri);
    }
}
