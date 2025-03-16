using System.Text.Json.Nodes;

namespace Elk.LanguageServer.Targets;

public abstract class Target
{
    public IReadOnlyDictionary<string, Func<JsonNode, object?>> Methods
        => _methods;

    public IReadOnlyDictionary<string, Action<JsonNode>> Notifications
        => _notifications;

    private readonly Dictionary<string, Func<JsonNode, object?>> _methods = new();
    private readonly Dictionary<string, Action<JsonNode>> _notifications = new();

    public void RegisterMethod(string method, Func<JsonNode, object?> callback)
    {
        _methods[method] = callback;
    }
    public void RegisterNotification(string method, Action<JsonNode> callback)

    {
        _notifications[method] = callback;
    }
}