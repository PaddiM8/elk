#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elk.Lexing;
using Elk.Parsing;
using Elk.ReadLine.Render.Formatting;

#endregion

namespace Elk.Interpreting.Exceptions;

public record Trace(TextPos Position, Token? FunctionIdentifier = null)
{
    internal Trace(TextPos position, Expr? enclosingFunction)
        : this(position, GetEnclosingFunctionName(enclosingFunction, position))
    {
    }

    private static Token? GetEnclosingFunctionName(Expr? enclosingFunction, TextPos position)
    {
        if (enclosingFunction == null)
            return null;

        return enclosingFunction is FunctionExpr functionExpr
            ? functionExpr.Identifier
            : new Token(TokenKind.Identifier, "<closure>", position);
    }
}

public class RuntimeException : Exception
{
    public TextPos? StartPosition { get; set; }

    public TextPos? EndPosition { get; set; }

    public List<Trace> ElkStackTrace { get; init; } = [];

    public RuntimeException(string message)
        : base(message)
    {
    }

    public RuntimeException(string message, TextPos? startPosition, TextPos? endPosition)
        : base(message)
    {
        StartPosition = startPosition;
        EndPosition = endPosition;

        if (startPosition != null)
            ElkStackTrace.Add(new Trace(startPosition));
    }

    public override string ToString()
        => ToString(includePosition: true);

    public string ToString(bool includePosition)
    {
        if (Message.Length == 0)
            return "";

        var builder = new StringBuilder();
        builder.Append(Ansi.Format("Error", AnsiForeground.Red) + ": ");
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

        if (!ElkStackTrace.Any() && StartPosition != null)
            builder.AppendLine(StartPosition.ToString());

        return builder.ToString().Trim();
    }
}