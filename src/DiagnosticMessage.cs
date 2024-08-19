using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Exceptions;
using Elk.Lexing;
using Elk.ReadLine.Render.Formatting;

namespace Elk;

public record DiagnosticMessage(string Message, TextPos StartPosition, TextPos EndPosition)
{
    public List<Trace> StackTrace { get; init; } = [];

    public override string ToString()
        => ToString(includePosition: true);

    public string ToString(bool includePosition)
    {
        if (Message.Length == 0)
            return "";

        var builder = new StringBuilder();
        builder.Append(Ansi.Format("Error", AnsiForeground.Red) + ": ");
        builder.AppendLine(Message);

        if (includePosition)
        {
            var position = StackTrace.Any()
                ? BuildStackTrace()
                : StartPosition.ToString();
            builder.AppendLine(position);
        }

        return builder.ToString().Trim();
    }

    private string BuildStackTrace()
    {
        var builder = new StringBuilder();
        foreach (var trace in StackTrace)
        {
            var position = $"{trace.Position} ";
            builder.AppendLine(
                $"{position}{trace.FunctionName}".Trim()
            );
        }

        return builder.ToString();
    }
}