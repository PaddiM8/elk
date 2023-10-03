using System;
using System.Collections.Generic;
using System.Linq;
using Elk.Interpreting;
using Elk.Std.Attributes;
using Elk.Std.DataTypes;
using Elk.Std.DataTypes.Serialization.CommandLine;

namespace Elk.Std;

[ElkModule("cli")]
static class Cli
{
    /// <param name="name">The name of the program/script/etc.</param>
    /// <returns>An empty CliParser that can be configured using other functions in this module. Has a --help flag by default.</returns>
    [ElkFunction("create")]
    public static RuntimeObject Create(RuntimeString name)
    {
        return new RuntimeCliParser(name.Value);
    }

    /// <summary>
    /// Adds a flag to the given parser.
    /// </summary>
    /// <param name="parser">The parser to add it to</param>
    /// <param name="flag">A dictionary such as the one in the example below</param>
    /// <returns>The parser that was given.</returns>
    /// <example>
    /// parser | cli::addFlag({
    ///     "identifier": "test-flag",  # Identifier used to later get the value of the flag (default: nil)
    ///     "shortName": "t",           # A short version of the flag (a single letter) (default: nil)
    ///     "longName": "test-flag",    # A long version of the flag (default: nil)
    ///     "description": "Test flag", # A description of the flag (default: nil)
    ///     "format": "hh:mm",          # An example of how to format the value given to the flag (default: nil)
    ///     "expects-value": true,      # Whether or not the flag expects a value (default: false)
    ///     "required": true,           # Whether or not the flag is required (default: false)
    /// })
    /// </example>
    [ElkFunction("addFlag")]
    public static RuntimeObject AddFlag(RuntimeCliParser parser, RuntimeDictionary flag)
    {
        parser.AddFlag(new CliFlag
        {
            Identifier = flag.GetExpectedValue<RuntimeString>("identifier").Value,
            ShortName = flag.GetValue<RuntimeString>("shortName")?.Value,
            LongName = flag.GetValue<RuntimeString>("longName")?.Value,
            Description = flag.GetValue<RuntimeString>("description")?.Value,
            Format = flag.GetValue<RuntimeString>("format")?.Value,
            ExpectsValue = flag.GetValue<RuntimeBoolean>("expectsValue")?.IsTrue ?? false,
            IsRequired = flag.GetValue<RuntimeBoolean>("required")?.IsTrue ?? false,
        });

        return parser;
    }

    /// <summary>
    /// Adds an argument to the given parser.
    /// </summary>
    /// <param name="parser">The parser to add it to</param>
    /// <param name="argument">A dictionary such as the one in the example below</param>
    /// <returns>The parser that was given.</returns>
    /// <example>
    /// parser | cli::addArgument({
    ///     "identifier": "test-flag", # Identifier used to later get the value of the flag (default: nil)
    ///     "description": "Some description", # A description of the flag (default: nil)
    ///     "required": true, # Whether or not the flag is required (default: false)
    ///     "variadic": true, # Whether or not the argument may consist of several tokens (default: false)
    /// })
    /// </example>
    [ElkFunction("addArgument")]
    public static RuntimeObject AddArgument(RuntimeCliParser parser, RuntimeDictionary argument)
    {
        var typedArgument = new CliArgument
        {
            Identifier = argument.GetExpectedValue<RuntimeString>("identifier").Value,
            Description = argument.GetValue<RuntimeString>("description")?.Value,
            IsRequired = argument.GetValue<RuntimeBoolean>("required")?.IsTrue ?? false,
            IsVariadic = argument.GetValue<RuntimeBoolean>("variadic")?.IsTrue ?? false,
        };
        parser.AddArgument(typedArgument);

        return parser;
    }

    /// <summary>
    /// Adds a verb to the given parser.
    /// Usage example: ./main.elk some-verb [flags] [arguments]
    /// </summary>
    /// <param name="parser">The parser to add it to</param>
    /// <param name="name">The name of the verb</param>
    /// <param name="closure">A handler for populating the verb's own CliParser</param>
    /// <returns>The parser that was given.</returns>
    /// <example>
    /// parser | cli::addVerb some-verb => someVerbParser: {
    ///     someVerbParser
    ///         | cli::addFlag({
    ///             "identifier": "some-flag",
    ///             "shortName": "s",
    ///         })
    ///         | cli::addFlag({
    ///             "identifier": "another-flag",
    ///             "shortName": "a",
    ///         })
    /// }
    /// </example>
    [ElkFunction("addVerb")]
    public static RuntimeObject AddVerb(RuntimeCliParser parser, RuntimeString name, Action<RuntimeObject> closure)
    {
        parser.AddVerb(name.Value, closure);

        return parser;
    }

    /// <summary>
    /// Sets a description for the parser.
    /// </summary>
    /// <param name="parser">The parser to modify</param>
    /// <param name="description">A description of what the program/script/verb/etc. does</param>
    /// <returns>The parser that was given.</returns>
    [ElkFunction("setDescription")]
    public static RuntimeObject SetDescription(RuntimeCliParser parser, RuntimeString description)
        => parser.SetDescription(description.Value);

    /// <summary>
    /// Causes the parser to ignore any flags placed after an argument, to
    /// parse it as an argument instead.
    /// </summary>
    /// <param name="parser">The parser to modify</param>
    /// <returns>The parser that was given.</returns>
    [ElkFunction("ignoreFlagsAfterArguments")]
    public static RuntimeObject IgnoreFlagsAfterArguments(RuntimeCliParser parser)
        => parser.IgnoreFlagsAfterArguments();


    /// <summary>
    /// Parses the given arguments using the given parser.
    /// </summary>
    /// <param name="parser">The parser to modify</param>
    /// <param name="args">A collection of strings</param>
    /// <returns>
    /// A dictionary containing the parsed values, where the identifiers given to flags
    /// and arguments are used as keys.
    /// </returns>
    [ElkFunction("parse")]
    public static RuntimeObject Parse(RuntimeCliParser parser, IEnumerable<RuntimeObject> args)
    {
        var result = parser.Run(args.Select(x => x.As<RuntimeString>().Value));
        if (result == null)
            return RuntimeNil.Value;

        var runtimeResult = result.Select(pair =>
        {
            var key = new RuntimeString(pair.Key);
            RuntimeObject value = pair.Value switch
            {
                IEnumerable<string> list => new RuntimeList(
                    list.Select(x => new RuntimeString(x))
                ),
                null => RuntimeNil.Value,
                _ => new RuntimeString(pair.Value.ToString() ?? ""),
            };

            return new KeyValuePair<int, (RuntimeObject, RuntimeObject)>(
                key.GetHashCode(),
                (key, value)
            );
        });

        return new RuntimeDictionary(
            new Dictionary<int, (RuntimeObject, RuntimeObject)>(runtimeResult)
        );
    }

    /// <summary>
    /// Parses the contents of the `argv` list using the given parser.
    /// </summary>
    /// <param name="parser">The parser to modify</param>
    /// <param name="env"></param>
    /// <returns>
    /// A dictionary containing the parsed values, where the identifiers given to flags
    /// and arguments are used as keys.
    /// </returns>
    [ElkFunction("parseArgv")]
    public static RuntimeObject ParseArgv(RuntimeCliParser parser, ShellEnvironment env)
        => Parse(parser, env.Argv.Skip(1));
}