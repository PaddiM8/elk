#region

using System.Collections.Generic;
using Elk.Analysis;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Scoping;
using Elk.Std.DataTypes;
using Elk.Vm;
using NUnit.Framework;
using static Elk.Tests.AstBuilder;

#endregion

namespace Elk.Tests;

internal class InterpreterTests
{
    private RuntimeObject Interpret(IList<Expr> ast, Scope? scope = null)
    {
        var virtualMachine = new VirtualMachine(
            new RootModuleScope(null, null),
            new VirtualMachineOptions()
        );
        var result = ElkProgram.Evaluate(
            new Ast(ast),
            scope ?? virtualMachine.RootModule,
            AnalysisScope.OncePerModule,
            virtualMachine
        );

        return result.Value ?? RuntimeNil.Value;
    }

    [TestCase(2, TokenKind.Plus, 3, 5)]
    [TestCase(2, TokenKind.Minus, 3, -1)]
    [TestCase(2, TokenKind.Star, 3, 6)]
    [TestCase(9, TokenKind.Slash, 2, 4.5)]
    [TestCase("hello", TokenKind.Plus, " world", "hello world")]
    [TestCase(5, TokenKind.Greater, 4.5, true)]
    [TestCase(5, TokenKind.Greater, 5, false)]
    [TestCase(5, TokenKind.GreaterEquals, 4.5, true)]
    [TestCase(5, TokenKind.GreaterEquals, 5, true)]
    [TestCase(5, TokenKind.Less, 4.5, false)]
    [TestCase(5, TokenKind.Less, 5, false)]
    [TestCase(5, TokenKind.LessEquals, 4.5, false)]
    [TestCase(5, TokenKind.LessEquals, 5, true)]
    [TestCase("hello", TokenKind.EqualsEquals, "hello", true)]
    [TestCase("hello", TokenKind.NotEquals, "world", true)]
    public void TestBinary(object left, TokenKind op, object right, object expectedResult)
    {
        var ast = Binary(
            Literal(left),
            op,
            Literal(right)
        );
        var result = Interpret([ast]);
        Assert.AreEqual(RuntimeValue(expectedResult).GetType(), result.GetType());
        Assert.True(SameResult(expectedResult, result));
    }

    [Test]
    public void TestUnary()
    {
        var ast = Unary(
            TokenKind.Minus,
            Literal(2)
        );
        var result = Interpret([ast]);
        Assert.True(SameResult(-2, result));
    }

    [Test]
    public void TestVariable()
    {
        var ast = new List<Expr>
        {
            Let("x", Literal(2)),
            Var("x"),
        };
        var scope = new RootModuleScope(null, new Ast(ast));
        scope.AddVariable("x", RuntimeNil.Value);

        var result = Interpret(ast, scope);
        Assert.True(SameResult(2, result));
    }

    private bool SameResult(object expectedResult, RuntimeObject gotResult)
        => ((RuntimeBoolean)RuntimeValue(expectedResult)
            .Operation(OperationKind.EqualsEquals, gotResult))
            .IsTrue;

    private static RuntimeObject RuntimeValue(object value)
        => value switch
        {
            true => RuntimeBoolean.True,
            false => RuntimeBoolean.False,
            int x => new RuntimeInteger(x),
            double x => new RuntimeFloat(x),
            string x => new RuntimeString(x),
            _ => RuntimeNil.Value,
        };
}