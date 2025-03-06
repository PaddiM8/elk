using System.Collections.Generic;
using System.Linq;
using Elk.Parsing;

namespace Elk.Services;

class SemanticTokenGenerator
{
    private readonly List<SemanticToken> _tokens = [];

    private SemanticTokenGenerator()
    {
    }

    public static IEnumerable<SemanticToken> GetSemanticTokens(IEnumerable<Expr> expressions)
    {
        var generator = new SemanticTokenGenerator();
        foreach (var expr in expressions)
        {
            generator.Next(expr);
        }

        return generator._tokens;
    }

    private void Next(Expr next)
    {
        switch (next)
        {
            case ModuleExpr expr:
                NextModule(expr);
                break;
            case StructExpr expr:
                NextStruct(expr);
                break;
            case FunctionExpr expr:
                NextFunction(expr);
                break;
            case LetExpr expr:
                NextLet(expr);
                break;
            case NewExpr expr:
                NextNew(expr);
                break;
            case ForExpr expr:
                NextFor(expr);
                break;
            case VariableExpr expr:
                NextVariable(expr);
                break;
            case CallExpr expr:
                NextCall(expr);
                break;
            case ClosureExpr expr:
                NextClosure(expr);
                break;
            case TryExpr expr:
                NextTry(expr);
                break;
            default:
                _tokens.AddRange(GetSemanticTokens(next.ChildExpressions));
                break;
        }
    }

    private void NextModule(ModuleExpr expr)
    {
        _tokens.Add(new SemanticToken(SemanticTokenKind.Module, expr.Identifier));
        _tokens.AddRange(GetSemanticTokens(expr.Body.ChildExpressions));
    }

    private void NextStruct(StructExpr expr)
    {
        _tokens.Add(new SemanticToken(SemanticTokenKind.Struct, expr.Identifier));
    }

    private void NextFunction(FunctionExpr expr)
    {
        _tokens.Add(new SemanticToken(SemanticTokenKind.Function, expr.Identifier));
        _tokens.AddRange(
            expr.Parameters.Select(parameter =>
                new SemanticToken(SemanticTokenKind.Parameter, parameter.Identifier)
            )
        );
        _tokens.AddRange(GetSemanticTokens(expr.ChildExpressions));
    }

    private void NextLet(LetExpr expr)
    {
        _tokens.AddRange(
            expr.IdentifierList.Select(identifier =>
                new SemanticToken(SemanticTokenKind.Variable, identifier)
            )
        );
        _tokens.AddRange(GetSemanticTokens(expr.ChildExpressions));
    }

    private void NextNew(NewExpr expr)
    {
        _tokens.AddRange(
            expr.ModulePath.Select(identifier =>
                new SemanticToken(SemanticTokenKind.Module, identifier)
            )
        );
        _tokens.Add(new SemanticToken(SemanticTokenKind.Struct, expr.Identifier));
        _tokens.AddRange(GetSemanticTokens(expr.ChildExpressions));
    }

    private void NextFor(ForExpr expr)
    {
        _tokens.AddRange(
            expr.IdentifierList.Select(identifier =>
                new SemanticToken(SemanticTokenKind.Variable, identifier)
            )
        );
        _tokens.AddRange(GetSemanticTokens(expr.ChildExpressions));
    }

    private void NextVariable(VariableExpr expr)
    {
        _tokens.Add(new SemanticToken(SemanticTokenKind.Variable, expr.Identifier));
    }

    private void NextCall(CallExpr expr)
    {
        _tokens.AddRange(
            expr.ModulePath.Select(identifier =>
                new SemanticToken(SemanticTokenKind.Module, identifier)
            )
        );
        _tokens.Add(new SemanticToken(SemanticTokenKind.Function, expr.Identifier));

        foreach (var argument in expr.Arguments)
        {
            if (expr.CallStyle != CallStyle.TextArguments)
            {
                Next(argument);
            }
            else if (argument is LiteralExpr literalExpr)
            {
                _tokens.Add(new SemanticToken(SemanticTokenKind.String, literalExpr.Value));
            }

            if (argument is not StringInterpolationExpr stringInterpolationExpr)
            {
                Next(argument);

                continue;
            }

            foreach (var part in stringInterpolationExpr.Parts)
            {
                if (part is LiteralExpr literal)
                {
                    _tokens.Add(new SemanticToken(SemanticTokenKind.String, literal.Value));

                    continue;
                }

                Next(part);
            }
        }
    }

    private void NextClosure(ClosureExpr expr)
    {
        Next(expr.Function);
        _tokens.AddRange(
            expr.Parameters.Select(identifier =>
                new SemanticToken(SemanticTokenKind.Variable, identifier)
            )
        );
        _tokens.AddRange(GetSemanticTokens(expr.Body.ChildExpressions));
    }

    private void NextTry(TryExpr expr)
    {
        _tokens.AddRange(GetSemanticTokens(expr.Body.ChildExpressions));
        foreach (var catchExpression in expr.CatchExpressions)
        {
            if (catchExpression.Identifier != null)
                _tokens.Add(new SemanticToken(SemanticTokenKind.Parameter, catchExpression.Identifier));

            if (catchExpression.Type != null)
                _tokens.Add(new SemanticToken(SemanticTokenKind.Struct, catchExpression.Type.Identifier));

            _tokens.AddRange(GetSemanticTokens(catchExpression.Body.ChildExpressions));
        }
    }
}
