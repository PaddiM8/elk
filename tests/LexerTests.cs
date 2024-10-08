#region

using System.Linq;
using Elk.Lexing;
using NUnit.Framework;

#endregion

namespace Elk.Tests;

internal class LexerTests
{
    [Test]
    public void TestEmpty()
    {
        var (tokens, diagnostics) = Lexer.Lex("", filePath: null);
        Assert.IsEmpty(tokens);
        Assert.IsEmpty(diagnostics);
    }

    [Test]
    public void TestBasics()
    {
        var (gotTokens, diagnostics) = Lexer.Lex("+-*/> >= < <= = == !=&&| ||(){}:,", filePath: null);
        Assert.IsEmpty(diagnostics);

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
            TokenKind.NotEquals,
            TokenKind.AmpersandAmpersand,
            TokenKind.Pipe,
            TokenKind.WhiteSpace,
            TokenKind.PipePipe,
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
        var (gotTokens, diagnostics) = Lexer.Lex("123.456 789 1", filePath: null);
        Assert.IsEmpty(diagnostics);

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
        var (gotTokens, diagnostics) = Lexer.Lex("\"hello world\" \"this is\n a test\"", filePath: null);
        Assert.IsEmpty(diagnostics);

        var expectedValues = new[]
        {
            ( TokenKind.DoubleQuoteStringLiteral, "hello world"),
            (TokenKind.WhiteSpace, " "),
            (TokenKind.DoubleQuoteStringLiteral, "this is\n a test"),
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
        var (gotTokens, diagnostics) = Lexer.Lex("123 # Comment\n123", filePath: null);
        Assert.IsEmpty(diagnostics);
        Assert.AreEqual(TokenKind.Comment, gotTokens[2].Kind);
        Assert.AreEqual("# Comment", gotTokens[2].Value);
    }

    [Test]
    public void TestIdentifier()
    {
        var (gotTokens, diagnostics) = Lexer.Lex("fn let if else for return nil true false notAKeyword", filePath: null);
        Assert.IsEmpty(diagnostics);

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