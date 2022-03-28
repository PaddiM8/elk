using System.Collections.Generic;
using System.Linq;
using Shel;
using Shel.Lexing;

static class AstBuilder
{
    public static Token Token(TokenKind kind, string value)
        => new(kind, value, new TextPos(0, 0));


}