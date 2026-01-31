using System.Text.Json.Serialization;

namespace Solaris.Rpc.RpcModels;

public class LatestBlockhash
{
    [JsonPropertyName("blockhash")] public required string Blockhash { get; set; }

    [JsonPropertyName("lastValidBlockHeight")]
    public int LastValidBlockHeight { get; set; }
}