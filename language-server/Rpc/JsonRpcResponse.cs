using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Elk.LanguageServer.Rpc;

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    public JsonNode? Result { get; set; }

    public JsonRpcError? Error { get; set; }

    public JsonNode? Id { get; set; }
}