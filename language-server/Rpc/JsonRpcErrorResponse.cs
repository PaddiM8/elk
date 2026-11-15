using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Elk.LanguageServer.Rpc;

public class JsonRpcErrorResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    public required JsonRpcError Error { get; set; }

    // Must be null if the request id couldn't be determined
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public JsonNode? Id { get; set; }
}
