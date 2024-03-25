#region

using System;
using System.Collections.Generic;
using Elk.Lexing;
using Elk.Parsing;

#endregion

namespace Elk.Exceptions;

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
}