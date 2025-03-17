using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Elk.LanguageServer.Lsp.Documents;
using Elk.LanguageServer.Targets;

namespace Elk.LanguageServer.Rpc;

public class JsonRpc
{
    public JsonSerializerOptions SerializerOptions { get; }

    private readonly Dictionary<string, Func<JsonNode, object?>> _methods = [];
    private readonly Dictionary<string, Action<JsonNode>> _notifications = [];
    private readonly BlockingCollection<object> _sendQueue = new(new ConcurrentQueue<object>());
    private readonly Stream _sendingStream;
    private readonly Stream _receivingStream;
    private readonly ILogger _logger;

    public JsonRpc(Stream sendingStream, Stream receivingStream, ILogger logger)
    {
        _sendingStream = sendingStream;
        _receivingStream = receivingStream;
        _logger = logger;

        SerializerOptions = new JsonSerializerOptions
        {
            TypeInfoResolver = RpcJsonContext.Default,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
        SerializerOptions.Converters.Add(new DocumentUriConverter());
    }

    public async Task StartListeningAsync(CancellationToken? cancellationToken = default)
    {
        var sendTask = Task.Run(() =>
        {
            try
            {
                foreach (var response in _sendQueue.GetConsumingEnumerable())
                    Send(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        });

        var receiveTask = Task.Run(() =>
        {
            try
            {
                using var reader = new StreamReader(_receivingStream, Encoding.UTF8);
                while (cancellationToken is not { IsCancellationRequested: true })
                    Receive(reader);

                _sendQueue.CompleteAdding();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        });

        await Task.WhenAll(sendTask, receiveTask);
    }

    public void RegisterTarget(Target target)
    {
        foreach (var (method, callback) in target.Methods)
            _methods[method] = callback;

        foreach (var (method, callback) in target.Notifications)
            _notifications[method] = callback;
    }

    public void Notify(string method, object parameters)
    {
        _sendQueue.Add(new JsonRpcRequest
        {
            Method = method,
            Parameters = JsonSerializer.SerializeToNode(parameters, SerializerOptions)!,
        });
    }

    private void Receive(StreamReader reader)
    {
        int? contentLength = null;
        while (true)
        {
            var line = reader.ReadLine();
            if (line == null)
                return;

            if (line == string.Empty)
                break;

            var parts = line.Split(":");
            if (parts[0].Equals("content-length", StringComparison.InvariantCultureIgnoreCase))
                contentLength = int.Parse(parts[1].Trim());
        }

        if (!contentLength.HasValue)
        {
            return;
        }

        var buffer = new char[contentLength.Value];
        reader.Read(buffer, 0, contentLength.Value);

        var response = JsonSerializer.Deserialize<JsonNode>(new string(buffer), SerializerOptions);
        if (response == null)
            return;

        if (response.GetValueKind() == JsonValueKind.Array)
        {
            foreach (var value in response.AsArray())
                HandleRequest(value.Deserialize<JsonRpcRequest>(SerializerOptions)!);

            return;
        }

        HandleRequest(response.Deserialize<JsonRpcRequest>(SerializerOptions)!);
    }

    private void HandleRequest(JsonRpcRequest request)
    {
        if (request.Id != null && _methods.TryGetValue(request.Method, out var callback))
        {
            var responseData = callback(request.Parameters ?? new JsonObject());
            var result = responseData == null
                ? new JsonObject()
                : JsonSerializer.SerializeToNode(responseData, SerializerOptions);
            var response = new JsonRpcResponse
            {
                Result = result,
                Id = request.Id,
            };

            _sendQueue.TryAdd(response);
        }
        else if (request.Id == null && _notifications.TryGetValue(request.Method, out var notificationCallback))
        {
            notificationCallback(request.Parameters ?? new JsonObject());
        }
        else
        {
            if (request.Method.StartsWith("$/") || request.Id == null)
                return;

            var response = new JsonRpcResponse
            {
                Error = new JsonRpcError
                {
                    Code = -32601,
                    Message = $"Method '{request.Method}' not found",
                },
                Id = request.Id,
            };

            _sendQueue.TryAdd(response);
        }
    }

    private void Send(object response)
    {
        var serialized = JsonSerializer.Serialize(response, SerializerOptions);

        var header = $"Content-Length: {serialized.Length}\r\n\r\n";
        _sendingStream.Write(Encoding.UTF8.GetBytes(header), 0, header.Length);
        _sendingStream.Write(Encoding.UTF8.GetBytes(serialized), 0, serialized.Length);
        _sendingStream.Flush();
    }
}