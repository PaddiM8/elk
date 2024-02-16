using Elk.Lexing;

namespace Elk;

public record DiagnosticMessage(string Message, TextPos StartPosition, TextPos EndPosition);