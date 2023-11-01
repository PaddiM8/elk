using System.IO;
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
        // Look for already loaded completions
        if (ParserStorage.CompletionParsers.TryGetValue(identifier, out var parser))
            return parser;

        // Look for completions in `/Resources/completions`
        var embedded = ResourceProvider.ReadFile($"completions/{identifier}.elk");
        _shellSession.RunCommand(embedded, ownScope: true, printReturnedValue: false);

        if (ParserStorage.CompletionParsers.TryGetValue(identifier, out parser))
            return parser;

        // Look for completions in ~/.config/elk/completions
        var completionFile = Path.Combine(CommonPaths.ConfigFolder, $"completions/{identifier}.elk");
        if (File.Exists(completionFile))
            _shellSession.RunCommand(File.ReadAllText(completionFile), ownScope: true, printReturnedValue: false);

        ParserStorage.CompletionParsers.TryGetValue(identifier, out parser);

        return parser;
    }
}