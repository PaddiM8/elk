using StreamJsonRpc;
using StreamJsonRpc.Protocol;

namespace Elk.LanguageServer;

class LanguageServerJsonRpc(
    Stream sendingStream,
    Stream receivingStream,
    IJsonRpcMessageFormatter formatter,
    object target)
    : JsonRpc(new HeaderDelimitedMessageHandler(sendingStream, receivingStream, formatter), target)
{
    protected override JsonRpcError.ErrorDetail CreateErrorDetails(JsonRpcRequest request, Exception exception)
    {
        var localRpcEx = exception as LocalRpcException;

        return new JsonRpcError.ErrorDetail
        {
            Code = (JsonRpcErrorCode?)localRpcEx?.ErrorCode ?? JsonRpcErrorCode.InvocationError,
            Message = exception.Message,
            Data = exception.StackTrace,
        };
    }
}
