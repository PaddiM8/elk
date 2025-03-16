using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Exceptions;
using Elk.ReadLine;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Vm;

namespace Elk.Std.Serialization.CommandLine;

public static class ParserStorage
{
    public static readonly Dictionary<string, RuntimeCliParser> InternalParser = new();

    public static readonly Dictionary<string, RuntimeCliParser> CompletionParsers = new();
}

[ElkType("CliParser")]
public class RuntimeCliParser(string name) : RuntimeObject
{
    public string Name { get; } = name;

    private string? _description;
    private bool _ignoreFlagsAfterArguments;
    private RuntimeCliParser? _parent;
    private Action<CliResult>? _action;
    private readonly Dictionary<string, RuntimeCliParser> _verbs = new();
    private readonly List<CliFlag> _flags = [];
    private readonly List<string> _requiredFlags = [];
    private readonly List<CliArgument> _arguments = [];
    private readonly List<string> _requiredArguments = [];

    public override RuntimeObject As(Type toType)
        => toType switch
        {
            _ when toType == typeof(RuntimeCliParser)
                => this,
            _ when toType == typeof(RuntimeString)
                => new RuntimeString(ToString()),
            _
                => throw new RuntimeCastException<RuntimeDictionary>(toType),
        };

    public override string ToString()
        => "CliParser";

    public static RuntimeCliParser LazyLoad(string name, Action<RuntimeCliParser> construct)
    {
        if (ParserStorage.InternalParser.TryGetValue(name, out var parser))
            return parser;

        var newParser = new RuntimeCliParser(name);
        construct(newParser);
        ParserStorage.InternalParser[name] = newParser;

        return newParser;
    }

    public RuntimeCliParser SetAction(Action<CliResult> action)
    {
        _action = action;

        return this;
    }

    public RuntimeCliParser AddVerb(string name, Action<RuntimeCliParser> setup)
    {
        var actionParser = new RuntimeCliParser(name)
        {
            _parent = this,
        };

        setup(actionParser);
        _verbs[name] = actionParser;

        return this;
    }

    public RuntimeCliParser AddFlag(CliFlag flag)
    {
        _flags.Add(flag);
        if (flag is { IsRequired: true, Identifier: not null })
            _requiredFlags.Add(flag.Identifier);

        return this;
    }

    public RuntimeCliParser AddArgument(CliArgument argument)
    {
        _arguments.Add(argument);
        if (argument is { IsRequired: true, Identifier: not null })
            _requiredArguments.Add(argument.Identifier);

        return this;
    }

    public RuntimeCliParser SetDescription(string description)
    {
        _description = description;

        return this;
    }

    public RuntimeCliParser IgnoreFlagsAfterArguments()
    {
        _ignoreFlagsAfterArguments = true;

        return this;
    }

    public IEnumerable<Completion> GetCompletions(
        string args,
        int? caret = null,
        CompletionKind completionKind = CompletionKind.Normal)
    {
        caret ??= args.Length;
        var whitespaceRegex = new System.Text.RegularExpressions.Regex(@"(?<!\\)\s");
        var tokensBeforeCaret = whitespaceRegex
            .Split(args[..caret.Value])
            .Select(x => Utils.Unescape(x).Trim());
        var allTokens = whitespaceRegex
            .Split(args)
            .Select(x => Utils.Unescape(x).Trim());

        try
        {
             return GetCompletions(
                tokensBeforeCaret.ToList(),
                Parse(allTokens, ignoreErrors: true),
                completionKind
            );
        }
        catch (RuntimeException)
        {
            // TODO: Might want to handle this
            return Array.Empty<Completion>();
        }
    }

    private IEnumerable<Completion> GetCompletions(
        ICollection<string> tokens,
        CliResult? cliResult,
        CompletionKind completionKind)
    {
        if (!tokens.Any())
            return Array.Empty<Completion>();

        var flagCompletions = _flags
            .Select(x =>
            {
                var shortName = x.ShortName == null
                    ? null
                    : "-" + x.ShortName;
                var longName = x.LongName == null
                    ? null
                    : "--" + x.LongName;
                var completionText = shortName ?? longName ?? "";
                var displayText = shortName != null && longName != null
                    ? string.Join(", ", shortName, longName)
                    : shortName + longName;

                return (
                    shortName: shortName ?? "",
                    longName: longName ?? "",
                    completion: new Completion(completionText, displayText, x.Description)
                );
            });

        // If there are no existing tokens, return all the verbs and flags
        if (!tokens.Any())
        {
            return _verbs
                .Select(x => new Completion(x.Key, x.Key, x.Value._description))
                .Concat(flagCompletions.Select(x => x.completion));
        }

        // If the first argument is a verb, let the verb's parser deal with the rest
        if (_verbs.TryGetValue(tokens.First(), out var verbParser))
            return verbParser.GetCompletions(tokens.Skip(1).ToList(), cliResult, completionKind);

        // If there is only one token and the parser contains verbs,
        // that means two things: the cursor is at the token, and
        // the token could be a verb. If these things are true,
        // and it isn't a flag, return the matched verbs.
        var last = tokens.Last();
        var matchedFlags = flagCompletions
            .Where(x => x.shortName.StartsWith(last) || x.longName.StartsWith(last))
            .Select(x =>
                // If the long name matches a completion, return that
                // completion but with CompletionText set to the long
                // name instead of the short name. Otherwise return
                // it as it is.
                !x.shortName.StartsWith(last) && x.longName.StartsWith(last)
                    ? x.completion with { CompletionText = x.longName }
                    : x.completion
            );
        var collectedTokens = tokens.ToList();
        if (collectedTokens.Count == 1 && _verbs.Any() && !last.StartsWith('-'))
        {
            // Also include matchedFlags, in case the token is empty,
            // which would mean it could either be a verb or a flag.
            return _verbs
                .Where(x => x.Key.StartsWith(collectedTokens.First()))
                .Select(x => new Completion(x.Key, x.Key, x.Value._description))
                .Concat(matchedFlags);
        }

        // If the second last token is a flag that expects a value, invoke the flag's
        // completion handler (if it exists).
        if (collectedTokens.Count >= 2)
        {
            var secondLast = collectedTokens[^2];
            CliFlag? flag = null;
            if (secondLast.StartsWith("--"))
            {
                flag = _flags.FirstOrDefault(x => x.LongName == secondLast[2..]);
            }
            else if (secondLast.StartsWith('-'))
            {
                flag = _flags.FirstOrDefault(x => x.ShortName == secondLast[1..]);
            }

            var allowCustom = completionKind != CompletionKind.Hint || flag?.AllowCustomCompletionHints is true;
            if (allowCustom && flag?.CompletionHandler != null && cliResult != null)
                return FilterCompletions(flag.CompletionHandler(last, cliResult), last);

            if (flag is { ValueKind: CliValueKind.Path or CliValueKind.Directory })
            {
                return FileUtils.GetPathCompletions(
                    last,
                    ShellEnvironment.WorkingDirectory,
                    flag.ValueKind == CliValueKind.Directory
                        ? FileType.Directory
                        : FileType.All,
                    completionKind
                );
            }

            if (flag is { ValueKind: not CliValueKind.None })
                return Array.Empty<Completion>();
        }

        // If the token that is currently being edited is going to be parsed as an argument,
        // find the relevant CliArgument and invoke the completion handler (if there is one).
        var argumentCount = cliResult?.ArgumentIndices
            .TakeWhile(x => x < collectedTokens.Count)
            .Count() ?? 0;
        var lastIsArgument = cliResult?.ArgumentIndices.Contains(tokens.Count() - 1) is true;
        var currentArgument = lastIsArgument
            ? _arguments.ElementAtOrDefault(argumentCount - 1) ?? _arguments.Last()
            : null;
        var allowCustomCompletion = completionKind != CompletionKind.Hint || currentArgument?.AllowCustomCompletionHints is true;
        if (allowCustomCompletion && currentArgument?.CompletionHandler != null && cliResult != null)
            return FilterCompletions(currentArgument.CompletionHandler(last, cliResult), last);

        // If none of the actions are relevant, simply return the matched flags.
        if (currentArgument is not { ValueKind: CliValueKind.Path or CliValueKind.Directory })
            return matchedFlags;

        var completions = FileUtils.GetPathCompletions(
            last,
            ShellEnvironment.WorkingDirectory,
            currentArgument.ValueKind == CliValueKind.Directory
                ? FileType.Directory
                : FileType.All
        );

        return last.StartsWith('-')
            ? completions.Concat(matchedFlags)
            : completions;
    }

    private IEnumerable<Completion> FilterCompletions(IEnumerable<Completion> completions, string query)
    {
        var collected = completions.ToList();
        var filtered = collected.Where(x => x.DisplayText.StartsWith(query));

        return filtered.Any()
            ? filtered
            : collected.Where(x => x.DisplayText.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    public CliResult? Parse(IEnumerable<string> args, bool ignoreErrors = false)
    {
        var values = new Dictionary<string, object?>();
        using var enumerator = args
            .Select(x => x.Trim())
            .WithIndex()
            .GetEnumerator();
        var isFirst = true;
        var variadicArgumentTokens = new List<string>();
        var argumentIndices = new List<int>();
        var argumentIndex = 0;
        var hasParsedArgument = false;
        while (enumerator.MoveNext())
        {
            var token = enumerator.Current.item;
            if (isFirst && _verbs.TryGetValue(token, out var verbParser))
                return verbParser.Parse(args.Skip(1), ignoreErrors);

            isFirst = false;
            var couldBeFlag = !_ignoreFlagsAfterArguments || !hasParsedArgument;
            if (couldBeFlag && (token.StartsWith("--") || token.StartsWith('-')))
            {
                var parsedFlag = ParseFlag(enumerator, ignoreErrors);
                if (parsedFlag == null)
                {
                    if (ignoreErrors)
                        continue;

                    return null;
                }

                if (!parsedFlag.Value.isFlag)
                {
                    if (parsedFlag.Value.identifier != null)
                        variadicArgumentTokens.Add(parsedFlag.Value.identifier);

                    continue;
                }

                if (parsedFlag.Value.identifier != null)
                    values[parsedFlag.Value.identifier] = parsedFlag.Value.value;

                continue;
            }

            if (argumentIndex == _arguments.Count - 1 && _arguments.Last().IsVariadic)
            {
                variadicArgumentTokens.Add(token);
                argumentIndices.Add(enumerator.Current.index);
                hasParsedArgument = true;
                continue;
            }

            if (argumentIndex >= _arguments.Count)
            {
                if (ignoreErrors)
                    continue;

                Console.Error.WriteLine($"Unexpected token: {token}");

                return null;
            }

            var identifier = _arguments[argumentIndex].Identifier;
            if (identifier != null)
                values[identifier] = token;

            hasParsedArgument = true;
            argumentIndices.Add(enumerator.Current.index);
            argumentIndex++;
        }

        if (variadicArgumentTokens.Any())
        {
            var lastIdentifier = _arguments.Last().Identifier;
            if (lastIdentifier != null)
                values[lastIdentifier] = variadicArgumentTokens;
        }

        var result = new CliResult(values, argumentIndices);
        if (ignoreErrors)
        {
            _action?.Invoke(result);
            return result;
        }

        // Error handling
        var missingRequiredFlags = _requiredFlags.Where(x => !values.ContainsKey(x));
        if (missingRequiredFlags.Any())
        {
            Console.Error.WriteLine($"Missing required flags: {string.Join(", ", missingRequiredFlags)}");
            return null;
        }

        var missingRequiredArguments = _requiredArguments.Where(x => !values.ContainsKey(x));
        if (missingRequiredArguments.Any())
        {
            Console.Error.WriteLine($"Missing required argument: {string.Join(", ", missingRequiredArguments)}");
            return null;
        }

        _action?.Invoke(result);

        return result;
    }

    private (string? identifier, string? value, bool isFlag)? ParseFlag(
        IEnumerator<(string item, int index)> enumerator,
        bool suppressOutput)
    {
        var givenFlag = enumerator.Current.item;
        if (!suppressOutput && givenFlag is "-h" or "--help")
        {
            ShowHelp();

            return null;
        }

        var flag = _flags.FirstOrDefault(x =>
            givenFlag.StartsWith("--")
                ? x.LongName == givenFlag[2..]
                : x.ShortName == givenFlag[1..]
        );

        if (flag == null)
        {
            if (suppressOutput)
                return (enumerator.Current.item, null, true);

            Console.Error.WriteLine($"Unrecognized flag: {givenFlag}");

            return null;
        }

        if (flag.ValueKind == CliValueKind.None)
            return (flag.Identifier, null, true);

        if (!enumerator.MoveNext() || enumerator.Current.item.StartsWith("-"))
        {
            if (suppressOutput)
                return (enumerator.Current.item, null, true);

            Console.Error.WriteLine($"Expected value for flag: {givenFlag}");

            return null;
        }

        return (flag.Identifier, enumerator.Current.item, true);
    }

    private void ShowHelp()
    {
        var builder = new StringBuilder();

        // Description
        if (_description != null)
        {
            builder.AppendLine(
                TextUtils.WrapWords(_description, Console.WindowWidth)
            );
        }

        // Usage
        builder.Append(Ansi.Bold(Ansi.Underline("Usage:")));
        builder.Append(' ');
        builder.AppendLine(BuildVerbHelp());
        foreach (var verb in _verbs)
        {
            builder.Append(new string(' ', "Usage: ".Length));
            builder.AppendLine(verb.Value.BuildVerbHelp());
        }

        // Arguments
        if (_arguments.Any())
            builder.AppendLine(Ansi.Bold(Ansi.Underline($"{System.Environment.NewLine}Arguments:")));

        foreach (var argument in _arguments)
            builder.AppendLine(BuildArgumentHelp(argument));

        // Options
        builder.AppendLine(Ansi.Bold(Ansi.Underline($"{System.Environment.NewLine}Options:")));

        var helpFlag = new CliFlag
        {
            Identifier = "help",
            ShortName = "h",
            LongName = "help",
            Description = "Print help",
        };

        foreach (var flag in _flags.Append(helpFlag))
            builder.AppendLine(BuildFlagHelp(flag));

        Console.WriteLine(builder.ToString().Trim());
    }

    private string BuildVerbHelp()
    {
        var builder = new StringBuilder();
        var ancestors = new List<string>
        {
            Name,
        };
        var selectedParser = this;
        while (selectedParser._parent != null)
        {
            selectedParser = selectedParser._parent;
            ancestors.Add(selectedParser.Name);
        }

        ancestors[^1] = Ansi.Bold(ancestors[^1]);

        builder.Append(string.Join(" ", Enumerable.Reverse(ancestors)));
        builder.Append(" [OPTIONS]");

        foreach (var argument in _arguments)
        {
            builder.Append($" [{argument.Identifier?.ToUpper()}]");

            if (argument.IsVariadic)
                builder.Append("...");
        }

        return builder.ToString();
    }

    private string BuildArgumentHelp(CliArgument argument)
    {
        var builder = new StringBuilder();
        var variadic = argument.IsVariadic
            ? "..."
            : "";
        builder.AppendLine($"  [{argument.Identifier?.ToUpper()}]{variadic}");

        if (argument.Description == null)
            return builder.ToString();

        builder.AppendLine(
            TextUtils.WrapWords(
                argument.Description,
                Console.WindowWidth,
                "\t"
            )
        );

        return builder.ToString().TrimEnd();
    }

    private string BuildFlagHelp(CliFlag flag)
    {
        var builder = new StringBuilder();
        var shortName = flag.ShortName == null
            ? "  "
            : $"-{flag.ShortName}";
        var longName = flag.LongName == null
            ? ""
            : $"--{flag.LongName}";
        var nameSeparator = flag is { ShortName: not null, LongName: not null }
            ? ", "
            : "  ";
        var format = flag.Format == null
            ? ""
            : $" <{flag.Format}>";
        var required = flag.IsRequired
            ? " (required)"
            : "";
        builder.AppendLine(Ansi.Bold($"  {shortName}{nameSeparator}{longName}{format}{required}"));

        if (flag.Description == null)
            return builder.ToString();

        builder.AppendLine(
            TextUtils.WrapWords(
                flag.Description,
                Console.WindowWidth,
                "\t"
            )
        );

        return builder.ToString().TrimEnd();
    }
}