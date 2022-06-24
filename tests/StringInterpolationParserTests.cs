#region

using System.Linq;
using Elk.Lexing;
using Elk.Parsing;
using NUnit.Framework;

#endregion

namespace Elk.Tests;

internal class StringInterpolationParserTests
{
    [Test]
    public void TestParseEmpty()
    {
        var parts = StringInterpolationParser.Parse(StringToken(""));
        Assert.IsEmpty(parts);
    }
    
    [Test]
    public void TestParsePureString()
    {
        var parts = StringInterpolationParser.Parse(StringToken("hello world"));
        Assert.AreEqual(1, parts.Count());
        Assert.AreEqual("hello world", parts.First().Value);
        Assert.AreEqual(InterpolationPartKind.Text, parts.First().Kind);
    }
    
    [Test]
    public void TestParseOnlyInterpolated()
    {
        var parts = StringInterpolationParser.Parse(StringToken("{hello}"));
        Assert.AreEqual(1, parts.Count());
        Assert.AreEqual("hello", parts.First().Value);
        Assert.AreEqual(InterpolationPartKind.Expression, parts.First().Kind);
    }
    
    [Test]
    public void TestParseInterpolated()
    {
        var parts = StringInterpolationParser.Parse(StringToken("abc{hello {world}}")).ToList();
        Assert.AreEqual(2, parts.Count());
        
        Assert.AreEqual("abc", parts[0].Value);
        Assert.AreEqual(InterpolationPartKind.Text, parts[0].Kind);
        
        Assert.AreEqual("hello {world}", parts[1].Value);
        Assert.AreEqual(InterpolationPartKind.Expression, parts[1].Kind);
    }

    private Token StringToken(string value)
        => new(TokenKind.StringLiteral, value, TextPos.Default);
}