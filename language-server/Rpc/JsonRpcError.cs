using System.Text.Json.Nodes;

namespace Elk.LanguageServer.Rpc;

public class JsonRpcError
{
    public required int Code { get; set; }

    public required string Message { get; set; }

    public JsonNode? Data { get; set; }
}