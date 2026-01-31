using System.Text.Json.Serialization;

namespace Solaris.Rpc.RpcModels;

public class BaseRpcResponse<T>
{
    [JsonPropertyName("result")] public T? Result { get; set; }
    [JsonPropertyName("error")] public SolanaRpcError? Error { get; set; }
    [JsonPropertyName("id")] public object? Id { get; set; }
}

public class ContextWrapped<T>
{
    [JsonPropertyName("context")] public RpcContext? Context { get; set; }

    [JsonPropertyName("value")] public T? Value { get; set; }
}

public class RpcContext
{
    [JsonPropertyName("apiVersion")] public string? ApiVersion { get; set; }

    [JsonPropertyName("slot")] public ulong Slot { get; set; }
}

public class SolanaRpcError
{
    [JsonPropertyName("code")] public int Code { get; set; }
    [JsonPropertyName("message")] public required string Message { get; set; }
}