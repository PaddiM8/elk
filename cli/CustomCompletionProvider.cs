using System;
using System.IO;
using Elk.Std.Serialization.CommandLine;

namespace Elk.Cli;

class CustomCompletionProvider(ShellSession shellSession)
{
    public RuntimeCliParser? Get(string identifier)
    {
        // Look for already loaded completions
        if (ParserStorage.CompletionParsers.TryGetValue(identifier, out var parser))
            return parser;

        // Look for default completions
        var embedded = ResourceProvider.ReadFile($"completions/{identifier}.elk");
        if (embedded != null)
            shellSession.RunCommand(embedded, ownScope: true, printReturnedValue: false);

        if (ParserStorage.CompletionParsers.TryGetValue(identifier, out parser))
            return parser;

        // Look for completions in ~/.config/elk/completions
        var completionFile = Path.Combine(CommonPaths.ConfigFolder, $"completions/{identifier}.elk");
        if (File.Exists(completionFile))
            shellSession.RunCommand(File.ReadAllText(completionFile), ownScope: true, printReturnedValue: false);

        ParserStorage.CompletionParsers.TryGetValue(identifier, out parser);

        return parser;
    }
}