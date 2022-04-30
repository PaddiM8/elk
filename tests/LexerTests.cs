using NUnit.Framework;
using Elk.Lexing;
using System.Linq;

namespace Elk.Tests;

internal class LexerTests
{
    [Test]
    public void TestEmpty()
    {
        Assert.IsEmpty(Lexer.Lex("", null));
    }

    [Test]
    public void TestBasics()
    {
        var gotTokens = Lexer.Lex("+-*/> >= < <= = == ! !=&&| ||(){}:,", null);
        var expectedKinds = new[]
        {
            TokenKind.Plus,
            TokenKind.Minus,
            TokenKind.Star,
            TokenKind.Slash,
            TokenKind.Greater,
            TokenKind.WhiteSpace,
            TokenKind.GreaterEquals,
            TokenKind.WhiteSpace,
            TokenKind.Less,
            TokenKind.WhiteSpace,
            TokenKind.LessEquals,
            TokenKind.WhiteSpace,
            TokenKind.Equals,
            TokenKind.WhiteSpace,
            TokenKind.EqualsEquals,
            TokenKind.WhiteSpace,
            TokenKind.Exclamation,
            TokenKind.WhiteSpace,
            TokenKind.NotEquals,
            TokenKind.And,
            TokenKind.Pipe,
            TokenKind.WhiteSpace,
            TokenKind.Or,
            TokenKind.OpenParenthesis,
            TokenKind.ClosedParenthesis,
            TokenKind.OpenBrace,
            TokenKind.ClosedBrace,
            TokenKind.Colon,
            TokenKind.Comma,
        };

        foreach (var (got, expected) in gotTokens.Zip(expectedKinds))
        {
            Assert.AreEqual(expected, got.Kind);
        }
    }

    [Test]
    public void TestNumber()
    {
        var gotTokens = Lexer.Lex("123.456 789 1", null);
        var expectedValues = new[]
        {
            (TokenKind.FloatLiteral, "123.456"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.IntegerLiteral, "789"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.IntegerLiteral, "1"),
        };

        foreach (var (got, (expectedKind, expectedValue)) in gotTokens.Zip(expectedValues))
        {
            Assert.AreEqual(expectedKind, got.Kind);
            Assert.AreEqual(expectedValue, got.Value);
        }
    }

    [Test]
    public void TestString()
    {
        var gotTokens = Lexer.Lex("\"hello world\" \"this is\n a test\"", null);
        var expectedValues = new[]
        {
            (TokenKind.StringLiteral, "hello world"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.StringLiteral, "this is\n a test"),
        };

        foreach (var (got, (expectedKind, expectedValue)) in gotTokens.Zip(expectedValues))
        {
            Assert.AreEqual(expectedKind, got.Kind);
            Assert.AreEqual(expectedValue, got.Value);
        }
    }

    [Test]
    public void TestComment()
    {
        var gotTokens = Lexer.Lex("123 # Comment\n123", null);
        Assert.AreEqual(TokenKind.Comment, gotTokens[2].Kind);
        Assert.AreEqual("# Comment", gotTokens[2].Value);
    }

    [Test]
    public void TestIdentifier()
    {
        var gotTokens = Lexer.Lex("fn let if else for return nil true false notAKeyword", null);
        var expectedValues = new[]
        {
            (TokenKind.Fn, "fn"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.Let, "let"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.If, "if"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.Else, "else"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.For, "for"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.Return, "return"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.Nil, "nil"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.True, "true"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.False, "false"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.Identifier, "notAKeyword"),
        };

        foreach (var (got, (expectedKind, expectedValue)) in gotTokens.Zip(expectedValues))
        {
            Assert.AreEqual(expectedKind, got.Kind);
            Assert.AreEqual(expectedValue, got.Value);
        }
    }
}