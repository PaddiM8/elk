#region

using System;
using System.Collections.Generic;
using System.Text;
using Elk.Lexing;
using Elk.ReadLine.Render.Formatting;

#endregion

namespace Elk.Interpreting.Exceptions;

public record Trace(TextPos Position, Token? FunctionIdentifier);

public class RuntimeException : Exception
{
    public TextPos? Position { get; set; }

    public List<Trace> ElkStackTrace { get; } = [];

    public RuntimeException(string message)
        : base(message)
    {
    }

    public RuntimeException(string message, TextPos? position)
        : base(message)
    {
        Position = position;
        if (position != null)
            ElkStackTrace.Add(new Trace(position, null));
    }

    public override string ToString()
        => ToString(includePosition: true);

    public string ToString(bool includePosition)
    {
        if (Message.Length == 0)
            return "";

        var builder = new StringBuilder();
        builder.Append(Ansi.Color("Error", AnsiForeground.Red) + ": ");
        builder.AppendLine(Message);

        foreach (var trace in ElkStackTrace)
        {
            var position = includePosition
                ? $"{trace.Position} "
                : "";
            builder.AppendLine(
                $"{position}{trace.FunctionIdentifier?.Value}".Trim()
            );
        }

        return builder.ToString().Trim();
    }
}