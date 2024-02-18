using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Interpreting.Exceptions;
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

        foreach (var trace in StackTrace)
        {
            var position = includePosition
                ? $"{trace.Position} "
                : "";
            builder.AppendLine(
                $"{position}{trace.FunctionIdentifier?.Value}".Trim()
            );
        }

        if (!StackTrace.Any())
            builder.AppendLine(StartPosition.ToString());

        return builder.ToString().Trim();
    }
}