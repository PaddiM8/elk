#region

using System.Collections.Generic;
using System.Text;
using Elk.Interpreting.Exceptions;
using Elk.Lexing;

#endregion

namespace Elk.Parsing;

enum InterpolationPartKind
{
    Text,
    Expression,
}

record InterpolationPart(string Value, InterpolationPartKind Kind);

class StringInterpolationParser
{
    public static IEnumerable<InterpolationPart> Parse(Token token)
    {
        var literal = token.Value;
        var textString = new StringBuilder();
        for (var i = 0; i < literal.Length; i++)
        {
            // Parse escaped dollar signs literally
            var next = literal.Length > i + 1
                ? literal[i + 1]
                : default;
            if (literal[i] == '\\' && next == '$')
            {
                i++;
                textString.Append('$');
                continue;
            }

            if (literal[i] == '$' && next == '{')
            {
                i++;
                if (textString.Length > 0)
                {
                    yield return new InterpolationPart(textString.ToString(), InterpolationPartKind.Text);
                    textString.Clear();
                }

                var expressionPart = NextExpressionPart(token, i + 1);
                yield return new InterpolationPart(expressionPart, InterpolationPartKind.Expression);
                i += expressionPart.Length + 1; // One additional for the closing brace

                continue;
            }

            textString.Append(literal[i]);
        }

        if (textString.Length > 0)
            yield return new InterpolationPart(textString.ToString(), InterpolationPartKind.Text);
    }

    private static string NextExpressionPart(Token token, int startIndex)
    {
        var (_, literal, textPos) = token;
        var exprString = new StringBuilder();
        var openBraceCount = 0;
        var i = startIndex;
        var inString = false;
        while (i < literal.Length && (openBraceCount > 0 || literal[i] != '}'))
        {
            if (i >= literal.Length)
                throw new RuntimeException("Expected '}' inside string literal", textPos);

            if (literal[i] == '"')
                inString = !inString;
            if (!inString && literal[i] == '{')
                openBraceCount++;
            else if (!inString && literal[i] == '}')
                openBraceCount--;

            exprString.Append(literal[i]);
            i++;
        }

        return exprString.ToString();
    }
}