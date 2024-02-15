using System.Collections.Immutable;
using Elk.Interpreting.Scope;
using Elk.Parsing;
using Elk.Services;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Elk.LanguageServer;

class SemanticDocument(string uri, string text)
{
    public string Uri { get; } = uri;

    public string Text { get; set; } = text;

    public Ast? Ast { get; set; }

    public SemanticTokens SemanticTokens { get; set; } = new()
    {
        Data = [],
    };

    public ModuleScope Module { get; } = new RootModuleScope(uri, null);

    public void Update(string text)
    {
        Text = text;
    }
}
