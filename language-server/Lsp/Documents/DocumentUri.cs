using System.Text;
using System.Text.RegularExpressions;

namespace Elk.LanguageServer.Lsp.Documents;

// https://github.com/OmniSharp/csharp-language-server-protocol/blob/master/src/Protocol/DocumentUri.cs
public class DocumentUri
{
    public string? Scheme { get; }

    public string Authority { get; }

    public string Path { get; }

    public string Query { get; }

    public string Fragment { get; }

    private static readonly Regex _regexp =
        new(@"^(([^:/?#]+?):)?(\/\/([^/?#]*))?([^?#]*)(\?([^#]*))?(#(.*))?");

    private static readonly Regex _encodedAsHexRegex =
        new("(%[0-9A-Za-z][0-9A-Za-z])+", RegexOptions.Multiline | RegexOptions.Compiled);
    
    private static readonly Regex _windowsPath =
        new(@"^\w(?:\:|%3a)[\\|\/]", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    
    private static readonly Dictionary<int, string> _encodeTable = new()
    {
        [CharCode.Colon] = "%3A",
        [CharCode.Slash] = "%2F",
        [CharCode.QuestionMark] = "%3F",
        [CharCode.Hash] = "%23",
        [CharCode.OpenSquareBracket] = "%5B",
        [CharCode.CloseSquareBracket] = "%5D",
        [CharCode.AtSign] = "%40",

        [CharCode.ExclamationMark] = "%21",
        [CharCode.DollarSign] = "%24",
        [CharCode.Ampersand] = "%26",
        [CharCode.SingleQuote] = "%27",
        [CharCode.OpenParen] = "%28",
        [CharCode.CloseParen] = "%29",
        [CharCode.Asterisk] = "%2A",
        [CharCode.Plus] = "%2B",
        [CharCode.Comma] = "%2C",
        [CharCode.Semicolon] = "%3B",
        [CharCode.Equals] = "%3D",

        [CharCode.Space] = "%20",
    };


    public DocumentUri(string? scheme, string? authority, string? path, string? query, string? fragment, bool? strict = null)
    {
        Scheme = SchemeFix(scheme, strict);
        Authority = authority ?? string.Empty;
        Path = ReferenceResolution(Scheme, path ?? string.Empty);
        Query = query ?? string.Empty;
        Fragment = fragment ?? string.Empty;
    }

    public static DocumentUri Parse(string value, bool strict = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UriFormatException("Given uri is null or empty");
        }

        var match = _regexp.Match(value);
        if (!match.Success)
        {
            return new DocumentUri(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
        }

        return new DocumentUri(
            match.Groups[2].Value,
            PercentDecode(match.Groups[4].Value),
            PercentDecode(match.Groups[5].Value),
            PercentDecode(match.Groups[7].Value),
            PercentDecode(match.Groups[9].Value),
            strict
        );
    }

    public static DocumentUri From(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            throw new UriFormatException("Given uri is null or empty");
        }

        if (url.StartsWith(@"\\") || url.StartsWith($"/") || _windowsPath.IsMatch(url))
        {
            return File(url);
        }

        return Parse(url);
    }

    public static DocumentUri File(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new UriFormatException("Given path is null or empty");

        var authority = string.Empty;

        if (path[0] != '/')
        {
            path = path.Replace('\\', '/');
        }

        if (path[0] == '/' && path[1] == '/')
        {
            var idx = path.IndexOf('/', 2);
            if (idx == -1)
            {
                authority = path[2..];
                path = "/";
            }
            else
            {
                authority = path.Substring(2, idx - 2);
                path = path[idx..];
                if (string.IsNullOrWhiteSpace(path))
                    path = "/";
            }
        }

        if (path.IndexOf("%3A", StringComparison.OrdinalIgnoreCase) > -1)
            path = path.Replace("%3a", ":").Replace("%3A", ":");

        return new DocumentUri("file", authority, path, string.Empty, string.Empty);
    }

    public void Deconstruct(
        out string? scheme, out string authority, out string path, out string query,
        out string fragment
    )
    {
        scheme = Scheme;
        authority = Authority;
        path = Path;
        query = Query;
        fragment = Fragment;
    }

    public override string ToString()
    {
        return AsFormatted(this, false);
    }

    private static string AsFormatted(DocumentUri uri, bool skipEncoding)
    {
        string Encoder(string p, bool allowSlash)
        {
            return !skipEncoding
                ? EncodeUriComponentFast(p, allowSlash)
                : EncodeUriComponentMinimal(p);
        }

        var res = new StringBuilder();
        var (scheme, authority, path, query, fragment) = uri;
        if (!string.IsNullOrWhiteSpace(scheme))
        {
            res.Append(scheme);
            res.Append(':');
        }

        if (!string.IsNullOrWhiteSpace(authority) || scheme == "file")
        {
            res.Append('/');
            res.Append('/');
        }

        if (!string.IsNullOrWhiteSpace(authority))
        {
            var idx = authority.IndexOf('@');
            if (idx != -1)
            {
                // <user>@<auth>
                var userinfo = authority[..idx];
                authority = authority[(idx + 1)..];
                idx = userinfo.IndexOf(':');
                if (idx == -1)
                {
                    res.Append(Encoder(userinfo, false));
                }
                else
                {
                    // <user>:<pass>@<auth>
                    res.Append(Encoder(userinfo[..idx], false));
                    res.Append(':');
                    res.Append(Encoder(userinfo[(idx + 1)..], false));
                }

                res.Append('@');
            }

            authority = authority.ToLowerInvariant();
            idx = authority.IndexOf(':');
            if (idx == -1)
            {
                res.Append(Encoder(authority, false));
            }
            else
            {
                // <auth>:<port>
                res.Append(Encoder(authority[..idx], false));
                res.Append(authority[idx..]);
            }
        }

        if (!string.IsNullOrWhiteSpace(path))
        {
            var appended = false;
            // Lower-case windows drive letters in /C:/fff or C:/fff
            if (path.Length >= 3 && path[0] == CharCode.Slash && path[2] == CharCode.Colon)
            {
                var code = path[1];
                if (code >= CharCode.A && code <= CharCode.Z)
                {
                    appended = true;
                    res.Append('/');
                    res.Append(Convert.ToChar(code + 32));
                    res.Append(':');
                    res.Append(Encoder(path[3..], true));
                }
            }
            else if (path.Length >= 2 && path[1] == CharCode.Colon)
            {
                var code = path[0];
                if (code >= CharCode.A && code <= CharCode.Z)
                {
                    appended = true;
                    res.Append(Convert.ToChar(code + 32));
                    res.Append(':');
                    res.Append(Encoder(path[2..], true));
                }
            }

            if (!appended)
            {
                res.Append(Encoder(path, true));
            }
        }

        if (!string.IsNullOrWhiteSpace(query))
        {
            res.Append('?');
            res.Append(Encoder(query, false));
        }

        if (!string.IsNullOrWhiteSpace(fragment))
        {
            res.Append('#');
            res.Append(!skipEncoding ? EncodeUriComponentFast(fragment, false) : fragment);
        }

        return res.ToString();
    }

    private static string EncodeUriComponentFast(string uriComponent, bool allowSlash)
    {
        StringBuilder? res = null;
        var nativeEncodePos = -1;

        for (var pos = 0; pos < uriComponent.Length; pos++)
        {
            var code = uriComponent[pos];

            // Unreserved characters: https://tools.ietf.org/html/rfc3986#section-2.3
            if ((code >= CharCode.a && code <= CharCode.z) ||
                (code >= CharCode.A && code <= CharCode.Z) ||
                (code >= CharCode.Digit0 && code <= CharCode.Digit9) ||
                code == CharCode.Dash ||
                code == CharCode.Period ||
                code == CharCode.Underline ||
                code == CharCode.Tilde ||
                (allowSlash && code == CharCode.Slash) ||
                (allowSlash && pos is 1 or 2 && (
                (uriComponent.Length >= 3 && uriComponent[0] == CharCode.Slash && uriComponent[2] == CharCode.Colon) ||
                    (uriComponent.Length >= 2 && uriComponent[1] == CharCode.Colon)
                ))
            )
            {
                // Check if we are delaying native encode
                if (nativeEncodePos != -1)
                {
                    res ??= new StringBuilder();
                    res.Append(Uri.EscapeDataString(uriComponent.AsSpan(nativeEncodePos, pos - nativeEncodePos)));
                    nativeEncodePos = -1;
                }

                // Check if we write into a new string (by default we try to return the param)
                res?.Append(uriComponent[pos]);
            }
            else
            {
                // Encoding needed, we need to allocate a new string
                if (res == null)
                {
                    res ??= new StringBuilder();
                    res.Append(uriComponent.AsSpan(0, pos));
                }

                // Check with default table first
                if (_encodeTable.TryGetValue(code, out var escaped))
                {
                    // Check if we are delaying native encode
                    if (nativeEncodePos != -1)
                    {
                        res.Append(Uri.EscapeDataString(uriComponent.AsSpan(nativeEncodePos, pos - nativeEncodePos)));
                        nativeEncodePos = -1;
                    }

                    // Append escaped variant to result
                    res.Append(escaped);
                }
                else if (nativeEncodePos == -1)
                {
                    // Use native encode only when needed
                    nativeEncodePos = pos;
                }
            }
        }

        if (nativeEncodePos != -1)
        {
            res ??= new StringBuilder();
            res.Append(Uri.EscapeDataString(uriComponent.AsSpan(nativeEncodePos)));
        }

        return res != null
            ? res.ToString()
            : uriComponent;
    }

    private static string EncodeUriComponentMinimal(string path)
    {
        StringBuilder? res = null;
        for (var pos = 0; pos < path.Length; pos++)
        {
            var code = path[pos];
            if (code == CharCode.Hash || code == CharCode.QuestionMark)
            {
                res ??= new StringBuilder(path[..pos]);
                res.Append(_encodeTable[code]);
            }
            else
            {
                res?.Append(path[pos]);
            }
        }

        return res != null ? res.ToString() : path;
    }

    private static string? SchemeFix(string? scheme, bool? strict)
    {
        if (string.IsNullOrWhiteSpace(scheme) && strict != true)
            return Uri.UriSchemeFile;

        return scheme;
    }


    private static string ReferenceResolution(string? scheme, string path)
    {
        if (scheme == Uri.UriSchemeHttps || scheme == Uri.UriSchemeHttp || scheme == Uri.UriSchemeFile)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "/";

            if (path[0] != '/')
                return '/' + path;
        }

        return path;
    }

    private static string DecodeUriComponentGraceful(string str)
    {
        try
        {
            return Uri.UnescapeDataString(str);
        }
        catch
        {
            if (str.Length > 3)
                return str[..3] + DecodeUriComponentGraceful(str[3..]);

            return str;
        }
    }

    private static string PercentDecode(string str)
    {
        return !_encodedAsHexRegex.IsMatch(str)
            ? str
            : _encodedAsHexRegex.Replace(str, match => DecodeUriComponentGraceful(match.Value));
    }
}