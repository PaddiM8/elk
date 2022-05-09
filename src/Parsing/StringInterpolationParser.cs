using System.Collections.Generic;
using System.Text;
using Elk.Lexing;

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
        string literal = token.Value;
        var textString = new StringBuilder();
        for (int i = 0; i < literal.Length; i++)
        {
            // Parse escaped braces literally
            if (literal[i] == '\\' && i + 1 < literal.Length && literal[i + 1] == '{')
            {
                i++;
                textString.Append('{');
                continue;
            }

            if (literal[i] == '{')
            {
                if (textString.Length > 0)
                {
                    yield return new(textString.ToString(), InterpolationPartKind.Text);
                    textString.Clear();
                }
                
                string expressionPart = NextExpressionPart(token, i + 1);
                yield return new(expressionPart, InterpolationPartKind.Expression);
                i += expressionPart.Length + 1; // One additional for the closing brace
                
                continue;
            }

            textString.Append(literal[i]);
        }
        
        if (textString.Length > 0)
            yield return new(textString.ToString(), InterpolationPartKind.Text);
    }

    private static string NextExpressionPart(Token token, int startIndex)
    {
        var (_, literal, textPos) = token;
        var exprString = new StringBuilder();
        int openBraceCount = 0;
        int i = startIndex;
        while (i < literal.Length && (openBraceCount > 0 || literal[i] != '}'))
        {
            if (i >= literal.Length)
                throw new ParseException(textPos, "Expected '}' inside string literal");
                
            if (literal[i] == '{')
                openBraceCount++;
            else if (literal[i] == '}')
                openBraceCount--;

            exprString.Append(literal[i]);
            i++;
        }

        return exprString.ToString();
    }
}