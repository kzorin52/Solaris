using System.Text.Json.Serialization;

namespace Solaris.Rpc.RpcModels;

public class SolanaAccount
{
    [JsonIgnore] public ReadOnlySpan<byte> DataSpan => Convert.FromBase64String(Data[0]);
    [JsonPropertyName("data")] public required string[] Data { get; set; }
    [JsonPropertyName("executable")] public bool Executable { get; set; }
    [JsonPropertyName("lamports")] public ulong Lamports { get; set; }
    [JsonPropertyName("owner")] public required string Owner { get; set; }
    [JsonPropertyName("rentEpoch")] public ulong RentEpoch { get; set; }
    [JsonPropertyName("space")] public ulong Space { get; set; }
}