#region

using System;
using System.Text;
using Elk.Highlighting;
using Elk.ReadLine;
using Elk.ReadLine.Render.Formatting;
using Elk.Services;

#endregion

namespace Elk.Cli;

class HighlightHandler(ShellSession shell) : IHighlightHandler
{
    public Highlighter Highlighter { get; } = new(shell.CurrentModule, shell);

    public string Highlight(string text, int caret)
    {
        var tokens = Highlighter.Highlight(text, caret);
        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            var color = token.Kind switch
            {
                SemanticTokenKind.None => AnsiForeground.Default,
                SemanticTokenKind.Module => AnsiForeground.DarkBlue,
                SemanticTokenKind.UnknownSymbol => AnsiForeground.Red,
                SemanticTokenKind.Type => AnsiForeground.Cyan,
                SemanticTokenKind.Struct => AnsiForeground.Cyan,
                SemanticTokenKind.Parameter => AnsiForeground.Default,
                SemanticTokenKind.Variable => AnsiForeground.Default,
                SemanticTokenKind.Function => AnsiForeground.Magenta,
                SemanticTokenKind.Keyword => AnsiForeground.Red,
                SemanticTokenKind.Comment => AnsiForeground.DarkGray,
                SemanticTokenKind.String => AnsiForeground.DarkYellow,
                SemanticTokenKind.TextArgument => AnsiForeground.DarkCyan,
                SemanticTokenKind.Path => AnsiForeground.DarkCyan,
                SemanticTokenKind.Number => AnsiForeground.DarkYellow,
                SemanticTokenKind.Operator => AnsiForeground.Default,
                SemanticTokenKind.InterpolationOperator => AnsiForeground.DarkGray,
                _ => throw new ArgumentOutOfRangeException()
            };

            string formatted;
            if (color == AnsiForeground.Default)
            {
                formatted = token.Value;
            }
            else if (token.Kind == SemanticTokenKind.Path)
            {
                formatted = Ansi.Format(token.Value, color, AnsiModifier.Underline);
            }
            else
            {
                formatted = Ansi.Format(token.Value, color);
            }

            builder.Append(formatted);
        }

        return builder.ToString();
    }
}
