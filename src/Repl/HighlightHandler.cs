using System.Linq;
using System.Text.RegularExpressions;
using BetterReadLine;

namespace Elk.Repl;

class HighlightHandler : IHighlightHandler
{
    private readonly ShellSession _shell;
    private readonly Regex _pattern;

    public HighlightHandler(ShellSession shell)
    {
        _shell = shell;

        const string textArgument = "(?<textArgument>( (?<!in\\b)[A-Za-z0-9\\-~.][^{})|\\n]+)?)";
        var rules = new[]
        {
            @"(?<keywords>\b(fn|if|else|return|include|let|true|false|for|in|nil|break|continue)\b)",
            @"(?<numbers>(?<!\w)\d+(\.\d+)?)",
            "(?<string>\"([^\"]|(?!\\\\)\")*\")",
            @"(?<comment>#.*(\n|\0))",
            @"(?<variableDeclaration>(?<=let |for )(\w+|\((\w+[ ]*,?[ ]*)*))",
            @"(?<path>([.~]?\/|\.\.\/|(\\[^{})|\s]|[^{})|\s])+\/)(\\.|[^{})|\s])+" + textArgument + ")",
            @$"(?<identifier>\b\w+{textArgument})",
        };
        _pattern = new Regex(string.Join("|", rules));
    }

    public string Highlight(string text)
    {
        string x = _pattern.Replace(text, m =>
        {
            int? colorCode = null;
            if (m.Groups["keywords"].Value.Any())
                colorCode = 31;
            if (m.Groups["numbers"].Value.Any())
                colorCode = 33;
            if (m.Groups["string"].Value.Any())
                colorCode = 93;
            if (m.Groups["comment"].Value.Any())
                colorCode = 90;
            if (m.Groups["variableDeclaration"].Value.Any())
                return m.Value;

            if (m.Groups["path"].Value.Any())
            {
                string argument = m.Groups["textArgument"].Value;
                string path = m.Groups["path"].Value;
                path = path[..^argument.Length];

                return argument.Any()
                    ? $"\x1b[32m{path}\x1b[94m{argument}\x1b[0m"
                    : $"\x1b[32m{path}\x1b[0m{argument}";
            }

            if (m.Groups["identifier"].Value.Any())
            {
                string argument = m.Groups["textArgument"].Value;
                string identifier = m.Groups["identifier"].Value;
                identifier = identifier[..^argument.Length];

                if (_shell.VariableExists(identifier))
                    return m.Groups["identifier"].Value;

                return argument.Any()
                    ? $"\x1b[32m{identifier}\x1b[94m{argument}\x1b[0m"
                    : $"\x1b[32m{identifier}\x1b[0m{argument}";
            }

            return colorCode == null
                ? m.Value
                : $"\x1b[{colorCode}m{m.Value}\x1b[0m";
        });

        return x;
    }
}