#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BetterReadLine;

#endregion

namespace Elk.Cli;

class HighlightHandler : IHighlightHandler
{
    private readonly ShellSession _shell;
    private readonly Regex _pattern;
    private readonly Regex _textArgumentPattern;
    private readonly Regex _stringLiteralPattern;

    public HighlightHandler(ShellSession shell)
    {
        _shell = shell;

        const string textArgumentPattern = "(?<textArgument>((&(?!&)|\\-(?!\\>)|\\>|\\\\[{})|&\\->;\\n]|\\$\\{[^}]+\\}|[^{})|&\\->;\\n])+)?)";
        
        const string singleQuoteStringPattern = "(?<singleQuoteString>\'((?<=\\\\)\'|[^\'])*\'?)";
        const string stringPattern = "(?<string>\"((?<=\\\\)\"|[^\"])*\"?)";
        const string interpolationPattern = @"(?<interpolation>\$\{[^}]+\})";
        var rules = new[]
        {
            @"(?<keywords>\b(module|struct|fn|if|else|return|with|using|from|let|new|true|false|for|while|in|nil|break|continue|and|or)\b)",
            @"(?<types>\b(Boolean|Dictionary|Error|Float|Integer|List|Nil|Range|Regex|String|Tuple|Type|Iterable|Indexable)\b)",
            @"(?<numbers>(?<!\w)\d+(\.\d+)?)",
            @"(?<comment>#.*(\n|\0))",
            singleQuoteStringPattern,
            stringPattern,
            @"(?<namedDeclaration>(?<=let |for |with |module |struct )(\w+|\((\w+[ ]*,?[ ]*)*))",
            @"(?<path>([.~]?\/|\.\.\/|(\\[^{})|\s]|[^{})|\s])+\/)(\\.|[^{})|\s])+ " + textArgumentPattern + ")",
            @$"(?<identifier>\b\w+( {textArgumentPattern})?)",
        };
        _pattern = new Regex(
            string.Join("|", rules),
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );

        var textArgumentRules = new[]
        {
            singleQuoteStringPattern,
            stringPattern,
            interpolationPattern,
        };
        _textArgumentPattern = new Regex(
            string.Join("|", textArgumentRules),
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );

        var stringLiteralRules = new[]
        {
            interpolationPattern,
        };
        _stringLiteralPattern = new Regex(
            string.Join("|", stringLiteralRules),
            RegexOptions.Compiled | RegexOptions.ExplicitCapture
        );
    }

    public string Highlight(string text)
    {
        return _pattern.Replace(text, m =>
        {
            int? colorCode = null;
            if (m.Groups["keywords"].Value.Any())
                colorCode = 31;
            if (m.Groups["from"].Value.Any())
                colorCode = 31;
            if (m.Groups["types"].Value.Any())
                colorCode = 96;
            else if (m.Groups["numbers"].Value.Any())
                colorCode = 33;
            else if (m.Groups["string"].Value.Any())
                return HighlightStringLiteral(m.Value);
            else if (m.Groups["singleQuoteString"].Value.Any())
                return HighlightStringLiteral(m.Value);
            else if (m.Groups["comment"].Value.Any())
                colorCode = 90;
            else if (m.Groups["namedDeclaration"].Value.Any())
                return m.Value;
            else if (m.Groups["path"].Value.Any())
            {
                string argument = m.Groups["textArgument"].Value;
                string path = m.Groups["path"].Value;
                path = path[..^argument.Length];

                return argument.Any()
                    ? $"\x1b[32m{path}\x1b[94m{argument}\x1b[0m"
                    : $"\x1b[32m{path}\x1b[0m{argument}";
            }
            else if (m.Groups["identifier"].Value.Any())
            {
                string argument = m.Groups["textArgument"].Value;
                string identifier = m.Groups["identifier"].Value;
                identifier = identifier[..^argument.Length];

                if (_shell.StructExists(identifier))
                    return $"\x1b[96m{m.Groups["identifier"].Value}\x1b[0m";

                if (_shell.VariableExists(identifier.Trim()))
                {
                    var highlightedArgument = argument.Any()
                        ? Highlight(argument)
                        : "";

                    return identifier + highlightedArgument;
                }

                return argument.Any()
                    ? $"\x1b[32m{identifier}{HighlightTextArguments(argument)}\x1b[0m"
                    : $"\x1b[32m{identifier}\x1b[0m";
            }

            return colorCode == null
                ? m.Value
                : $"\x1b[{colorCode}m{m.Value}\x1b[0m";
        });
    }

    private string HighlightTextArguments(string textArguments)
    {
        string result = _textArgumentPattern.Replace(textArguments, m =>
        {
            if (m.Groups["string"].Value.Any() ||
                m.Groups["singleQuoteString"].Value.Any())
            {
                return $"{HighlightStringLiteral(m.Value)}\x1b[94m";
            }

            if (m.Groups["interpolation"].Value.Any())
            {
                string interpolation = Highlight(m.Value[2..^1]);
                return $"\x1b[37m${{\x1b[0m{interpolation}\x1b[37m}}\x1b[94m";
            }

            return m.Value;
        });

        return $"\x1b[94m{result}\x1b[0m";
    }

    private string HighlightStringLiteral(string text)
    {
        string result = _stringLiteralPattern.Replace(text, m =>
        {
            if (m.Groups["interpolation"].Value.Any())
            {
                string interpolation = Highlight(m.Value[2..^1]);

                return $"\x1b[37m${{\x1b[0m{interpolation}\x1b[37m}}\x1b[93m";
            }

            return m.Value;
        });

        return $"\x1b[93m{result}\x1b[0m";
    }
}
