using NUnit.Framework;
using Elk.Lexing;
using Elk.Parsing;
using Elk.Interpreting;
using System.Collections.Generic;
using System.Linq;
using static AstBuilder;

namespace Elk.Tests;

internal class ParserTests
{
    private static GlobalScope _scope = new();

    [SetUp]
    public void SetUp()
    {
        _scope = new GlobalScope();
    }

    [Test]
    public void TestBinary()
    {
        var tokens = new List<Token>()
        {
            Token(TokenKind.IntegerLiteral, "2"),
            Token(TokenKind.Plus, "+"),
            Token(TokenKind.IntegerLiteral, "3"),
            Token(TokenKind.Star, "*"),
            Token(TokenKind.IntegerLiteral, "7"),
            Token(TokenKind.EqualsEquals, "=="),
            Token(TokenKind.IntegerLiteral, "11"),
            Token(TokenKind.Minus, "/"),
            Token(TokenKind.IntegerLiteral, "13"),
        };
        dynamic ast = Parser.Parse(tokens, _scope);
        Assert.AreEqual("7", ast[0].Left.Right.Right.Value.Value);
        Assert.AreEqual("13", ast[0].Right.Right.Value.Value);
    }

    [Test]
    public void TestUnary()
    {
        var tokens = new List<Token>()
        {
            Token(TokenKind.Minus, "-"),
            Token(TokenKind.IntegerLiteral, "2"),
        };
        dynamic ast = Parser.Parse(tokens, _scope);
        Assert.AreEqual(TokenKind.Minus, ast[0].Operator);
        Assert.AreEqual("2", ast[0].Value.Value.Value);
    }

    [Test]
    public void TestVariable()
    {
        var tokens = new List<Token>()
        {
            Token(TokenKind.Identifier, "var"),
        };
        _scope.AddVariable("var", RuntimeNil.Value);
        dynamic ast = Parser.Parse(tokens, _scope);
        Assert.IsInstanceOf<VariableExpr>(ast[0]);
        Assert.AreEqual("var", ast[0].Identifier.Value);
    }

    [Test]
    public void TestBashStyleCall()
    {
        var tokens = new List<Token>()
        {
            Token(TokenKind.OpenParenthesis, "("),
            Token(TokenKind.Identifier, "echo"),
            Token(TokenKind.WhiteSpace, " "),
            Token(TokenKind.Plus, "+"),
            Token(TokenKind.WhiteSpace, " "),
            Token(TokenKind.Identifier, "hello"),
            Token(TokenKind.ClosedParenthesis, ")"),
            Token(TokenKind.Plus, "+"),
            Token(TokenKind.Identifier, "world"),
        };
        dynamic ast = Parser.Parse(tokens, _scope);
        dynamic left = ast[0].Left;
        Assert.IsInstanceOf<CallExpr>(left);
        Assert.AreEqual("echo", left.Identifier.Value);
        Assert.AreEqual(2, left.Arguments.Count);
        Assert.AreEqual("+", left.Arguments[0].Value.Value);
        Assert.AreEqual("hello", left.Arguments[1].Value.Value);

        dynamic right = ast[0].Right;
        Assert.AreEqual("world", right.Identifier.Value);
        Assert.IsEmpty(right.Arguments);
    }

    [Test]
    public void TestParenthesizedCall()
    {
        var tokens = new List<Token>()
        {
            Token(TokenKind.Identifier, "echo"),
            Token(TokenKind.OpenParenthesis, "("),
            Token(TokenKind.StringLiteral, "hello"),
            Token(TokenKind.Comma, ","),
            Token(TokenKind.StringLiteral, "world"),
            Token(TokenKind.ClosedParenthesis, ")"),
        };
        dynamic ast = Parser.Parse(tokens, _scope);
        Assert.IsInstanceOf<CallExpr>(ast[0]);
        Assert.AreEqual("echo", ast[0].Identifier.Value);
        Assert.AreEqual(2, ast[0].Arguments.Count);
        Assert.AreEqual("hello", ast[0].Arguments[0].Value.Value);
        Assert.AreEqual("world", ast[0].Arguments[1].Value.Value);
    }

    [Test]
    public void TestIf()
    {
        var tokens = new List<Token>()
        {
            Token(TokenKind.If, "if"),
            Token(TokenKind.True, "true"),
            Token(TokenKind.Colon, ":"),
            Token(TokenKind.IntegerLiteral, "2"),
            Token(TokenKind.Else, "else"),
            Token(TokenKind.OpenBrace, "{"),
            Token(TokenKind.IntegerLiteral, "3"),
            Token(TokenKind.ClosedBrace, "}"),
        };
        dynamic ast = Parser.Parse(tokens, _scope);
        Assert.IsInstanceOf<IfExpr>(ast[0]);
        Assert.AreEqual("true", ast[0].Condition.Value.Value);
        Assert.AreEqual("2", ast[0].ThenBranch.Expressions[0].Value.Value);
        Assert.AreEqual("3", ast[0].ElseBranch.Expressions[0].Value.Value);
    }

    [Test]
    public void TestFunction()
    {
        var tokens = new List<Token>()
        {
            Token(TokenKind.Fn, "fn"),
            Token(TokenKind.Identifier, "main"),
            Token(TokenKind.OpenParenthesis, "("),
            Token(TokenKind.Identifier, "x"),
            Token(TokenKind.Comma, ","),
            Token(TokenKind.Identifier, "y"),
            Token(TokenKind.ClosedParenthesis, ")"),
            Token(TokenKind.Colon, ":"),
            Token(TokenKind.IntegerLiteral, "2"),
        };
        dynamic ast = Parser.Parse(tokens, _scope);
        Assert.IsInstanceOf<FunctionExpr>(ast[0]);
        Assert.AreEqual("main", ast[0].Identifier.Value);
        Assert.AreEqual(2, ast[0].Parameters.Count);
        Assert.AreEqual("x", ast[0].Parameters[0].Value);
        Assert.AreEqual("y", ast[0].Parameters[1].Value);
        Assert.AreEqual("2", ast[0].Block.Expressions[0].Value.Value);
    }
}