namespace Shel.Lexing;

internal enum TokenKind
{
    Unknown,

    // Keywords
    Fn, Let, If, Else, For, In, Return, Break, Continue, Include,
    Nil, True, False,

    // Operators
    Plus, Minus, Star, Slash,
    Exclamation, Greater, Less, GreaterEquals, LessEquals, EqualsEquals, NotEquals,
    Equals,
    And, Or,
    Pipe,

    // Brackets
    OpenParenthesis, ClosedParenthesis,
    OpenSquareBracket, ClosedSquareBracket,
    OpenBrace, ClosedBrace,

    // Punctuation
    Comma, Colon, Hash, Dot, DotDot, Tilde,

    // Other
    IntegerLiteral, FloatLiteral, StringLiteral,
    NewLine, WhiteSpace,
    Comment,
    Identifier,
    EndOfFile,
}