using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Elk.Std.DataTypes.Serialization.CommandLine;

static class ParserStorage
{
    public static readonly Dictionary<string, object> Parsers = new();
}

public class CommandLineParser<T>
{
    private readonly string _name;
    private string? _description;
    private CommandLineParser<T>? _parent;
    private readonly Dictionary<string, CommandLineParser<T>> _verbs = new();
    private readonly List<CommandLineFlag> _flags = new();
    private readonly List<string> _requiredFlags = new();
    private readonly List<CommandLineArgument> _arguments = new();
    private readonly List<string> _requiredArguments = new();
    private Func<CommandLineResult, T>? _action;

    public CommandLineParser(string name)
    {
        _name = name;
    }

    public static CommandLineParser<T> LazyLoad(string name, Action<CommandLineParser<T>> construct)
    {
        if (ParserStorage.Parsers.TryGetValue(name, out var parser))
            return (CommandLineParser<T>)parser;

        var newParser = new CommandLineParser<T>(name);
        construct(newParser);
        ParserStorage.Parsers[name] = newParser;

        return newParser;
    }

    public CommandLineParser<T> AddVerb(string name, Action<CommandLineParser<T>> setup)
    {
        var actionParser = new CommandLineParser<T>(name)
        {
            _parent = this,
        };

        setup(actionParser);
        _verbs[name] = actionParser;

        return this;
    }

    public CommandLineParser<T> AddFlag(CommandLineFlag flag)
    {
        _flags.Add(flag);
        if (flag.IsRequired)
            _requiredFlags.Add(flag.Identifier);

        return this;
    }

    public CommandLineParser<T> AddArgument(CommandLineArgument argument)
    {
        _arguments.Add(argument);
        if (argument.IsRequired)
            _requiredArguments.Add(argument.Identifier);

        return this;
    }

    public CommandLineParser<T> SetAction(Func<CommandLineResult, T> action)
    {
        _action = action;

        return this;
    }

    public CommandLineParser<T> SetDescription(string description)
    {
        _description = description;

        return this;
    }

    public bool Run(IEnumerable<string> arguments, out T? result)
    {
        result = default;

        var values = new Dictionary<string, object?>();
        using var enumerator = arguments.GetEnumerator();
        bool isFirst = true;
        var variadicArgumentTokens = new List<string>();
        int argumentIndex = 0;
        while (enumerator.MoveNext())
        {
            var token = enumerator.Current;

            if (isFirst && _verbs.TryGetValue(token, out var verbParser))
                return verbParser.Run(arguments.Skip(1), out result);

            isFirst = false;
            if (token.StartsWith("--") || token.StartsWith("-"))
            {
                var parsedFlag = ParseFlag(enumerator);
                if (parsedFlag == null)
                    return false;

                values[parsedFlag.Value.identifier] = parsedFlag.Value.value;
                continue;
            }

            if (argumentIndex == _arguments.Count - 1 && _arguments.Last().IsVariadic)
            {
                variadicArgumentTokens.Add(token);
                continue;
            }

            if (argumentIndex >= _arguments.Count)
            {
                Console.Error.WriteLine($"Unexpected token: {token}");

                return false;
            }

            values[_arguments[argumentIndex].Identifier] = token;
            argumentIndex++;
        }

        if (variadicArgumentTokens.Any())
            values[_arguments.Last().Identifier] = variadicArgumentTokens;

        if (_verbs.Any())
        {
            Console.Error.WriteLine($"Expected one of: {string.Join(", ", _verbs.Keys)}");

            return false;
        }

        var missingRequiredFlags = _requiredFlags.Where(x => !values.ContainsKey(x));
        if (missingRequiredFlags.Any())
        {
            Console.Error.WriteLine($"Missing required flags: {string.Join(", ", missingRequiredFlags)}");

            return false;
        }

        var missingRequiredArguments = _requiredArguments.Where(x => !values.ContainsKey(x));
        if (missingRequiredArguments.Any())
        {
            Console.Error.WriteLine($"Missing required argument: {string.Join(", ", missingRequiredArguments)}");

            return false;
        }

        if (_action != null)
            result = _action(new CommandLineResult(values));

        return true;
    }

    private (string identifier, string? value)? ParseFlag(IEnumerator<string> enumerator)
    {
        var givenFlag = enumerator.Current;
        if (givenFlag is "-h" or "--help")
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
            Console.Error.WriteLine($"Unrecognized flag: {givenFlag}");

            return null;
        }

        if (!flag.ExpectsValue)
            return (flag.Identifier, null);

        if (!enumerator.MoveNext() || enumerator.Current.StartsWith("-"))
        {
            Console.Error.WriteLine($"Expected value for flag: {givenFlag}");

            return null;
        }

        return (flag.Identifier, enumerator.Current);
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
            builder.AppendLine(Ansi.Bold(Ansi.Underline("\nArguments:")));

        foreach (var argument in _arguments)
            builder.AppendLine(BuildArgumentHelp(argument));

        // Options
        builder.AppendLine(Ansi.Bold(Ansi.Underline("\nOptions:")));

        var helpFlag = new CommandLineFlag
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
            _name,
        };
        var selectedParser = this;
        while (selectedParser._parent != null)
        {
            selectedParser = selectedParser._parent;
            ancestors.Add(selectedParser._name);
        }

        ancestors[^1] = Ansi.Bold(ancestors[^1]);

        builder.Append(string.Join(" ", Enumerable.Reverse(ancestors)));
        builder.Append(" [OPTIONS]");

        foreach (var argument in _arguments)
        {
            builder.Append($" [{argument.Identifier.ToUpper()}]");
            if (argument.IsVariadic)
                builder.Append("...");
        }

        return builder.ToString();
    }

    private string BuildArgumentHelp(CommandLineArgument argument)
    {
        var builder = new StringBuilder();
        var variadic = argument.IsVariadic
            ? "..."
            : "";
        builder.AppendLine($"  [{argument.Identifier.ToUpper()}]{variadic}");

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

    private string BuildFlagHelp(CommandLineFlag flag)
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