using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Elk.LanguageServer.Rpc;

public class JsonRpcResponse
{
    [JsonPropertyName("jsonrpc")]
    public string JsonRpc { get; set; } = "2.0";

    // A successful response must always contain a result, even if it is null
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public JsonNode? Result { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public JsonNode? Id { get; set; }
}
