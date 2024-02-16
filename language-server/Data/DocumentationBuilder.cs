using System.Text;
using System.Text.RegularExpressions;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace Elk.LanguageServer.Data;

static class DocumentationBuilder
{
    public static MarkedString BuildSignature(
        string name,
        IEnumerable<string> parameters,
        string? documentation,
        bool hasClosure
        )
    {
        var signatureStringBuilder = new StringBuilder();

        // Documentation
        if (!string.IsNullOrWhiteSpace(documentation))
        {
            documentation = Regex.Replace(documentation, @"\n+\s*", "\n# ");
            signatureStringBuilder.AppendLine($"# {documentation}");
        }

        // Name
        signatureStringBuilder.Append($"fn {name}(");

        // Parameters
        signatureStringBuilder.Append(string.Join(", ", parameters));
        signatureStringBuilder.Append(')');

        if (hasClosure)
            signatureStringBuilder.Append(" => closure");

        return new MarkedString("elk", signatureStringBuilder.ToString());
    }
}