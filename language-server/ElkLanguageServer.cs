using Elk.LanguageServer.Targets;
using Newtonsoft.Json.Serialization;
using StreamJsonRpc;

namespace Elk.LanguageServer;

public static class ElkLanguageServer
{
    public static async Task StartAsync()
    {
        var formatter = new JsonMessageFormatter();
        formatter.JsonSerializer.ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new CamelCaseNamingStrategy()
        };

        var rpc = new LanguageServerJsonRpc(
            Console.OpenStandardOutput(),
            Console.OpenStandardInput(),
            formatter,
            null!
        );
        var targetOptions = new JsonRpcTargetOptions
        {
            UseSingleObjectParameterDeserialization = true,
        };

        rpc.AddLocalRpcTarget(new RootTarget(), targetOptions);
        rpc.AddLocalRpcTarget(new TextDocumentTarget(rpc), targetOptions);
        rpc.StartListening();
        await rpc.Completion;
    }
}
