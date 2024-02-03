using Elk.Interpreting.Scope;

namespace Elk.LanguageServer;

class SemanticDocument(string uri, string text)
{
    public string Uri { get; } = uri;

    public string Text { get; set; } = text;

    public ModuleScope Module { get; } = new RootModuleScope(uri, null);

    public void Update(string text)
    {
        Text = text;
    }
}
