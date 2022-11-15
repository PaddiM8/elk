#region

using System.Linq;
using System.Text.RegularExpressions;
using BetterReadLine;

#endregion

namespace Elk.Cli;

class HighlightHandler : IHighlightHandler
{
    private readonly ShellSession _shell;
    private readonly Regex _pattern;

    public HighlightHandler(ShellSession shell)
    {
        _shell = shell;

        const string textArgument = "(?<textArgument>([A-Za-z0-9\\-~.](&(?!&)|\\-(?!\\>)|\\>|\\\\[{})|&\\->;\\n]|[^{})|&\\->;\\n])+)?)";
        var rules = new[]
        {
            @"(?<keywords>\b(module|struct|fn|if|else|return|with|using|from|let|new|true|false|for|while|in|nil|break|continue|and|or)\b)",
            @"(?<types>\b(Boolean|Dictionary|Error|Float|Integer|List|Nil|Range|String|Tuple|Type)\b)",
            @"(?<numbers>(?<!\w)\d+(\.\d+)?)",
            "(?<string>\"((?<=\\\\)\"|[^\"])*\"?)",
            @"(?<comment>#.*(\n|\0))",
            @"(?<namedDeclaration>(?<=let |for |with |module |struct )(\w+|\((\w+[ ]*,?[ ]*)*))",
            @"(?<path>([.~]?\/|\.\.\/|(\\[^{})|\s]|[^{})|\s])+\/)(\\.|[^{})|\s])+ " + textArgument + ")",
            @$"(?<identifier>\b\w+ {textArgument})",
        };
        _pattern = new Regex(string.Join("|", rules), RegexOptions.Compiled | RegexOptions.ExplicitCapture);
    }

    public string Highlight(string text)
    {
        string x = _pattern.Replace(text, m =>
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
                colorCode = 93;
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