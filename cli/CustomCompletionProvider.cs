using Elk.Std.DataTypes.Serialization.CommandLine;

namespace Elk.Cli;

class CustomCompletionProvider
{
    private readonly ShellSession _shellSession;

    public CustomCompletionProvider(ShellSession shellSession)
    {
        _shellSession = shellSession;
    }

    public RuntimeCliParser? Get(string identifier)
    {
        if (ParserStorage.CompletionParsers.TryGetValue(identifier, out var parser))
            return parser;

        var embedded = EmbeddedResourceProvider.ReadAllText($"Completions.{identifier}.elk");
        if (embedded != null)
            _shellSession.RunCommand(embedded, ownScope: true, printReturnedValue: false);

        ParserStorage.CompletionParsers.TryGetValue(identifier, out parser);

        return parser;
    }
}