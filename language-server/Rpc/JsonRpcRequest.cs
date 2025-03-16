using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Elk.LanguageServer.Rpc;

public class JsonRpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    public required string Method { get; set; }

    [JsonPropertyName("params")]
    public JsonNode? Parameters { get; set; }

    public JsonNode? Id { get; set; }
}