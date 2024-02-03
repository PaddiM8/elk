using Elk.Interpreting.Scope;

namespace Elk.LanguageServer;

class SemanticDocument
{
    public string Uri { get; }

    public string Text { get; set; }

    public ModuleScope Module { get; }

    public SemanticDocument(string uri, string text)
    {
        Uri = uri;
        Text = text;
        Module = new RootModuleScope(uri, null);
        ElkProgram.Evaluate(text, Module, Analysis.AnalysisScope.OverwriteExistingModule);
    }

    public void Update(string text)
    {
        Text = text;
        ElkProgram.Evaluate(text, Module, Analysis.AnalysisScope.OverwriteExistingModule);
    }
}
