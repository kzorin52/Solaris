using System.Text.Json.Serialization;
using Solaris.Base.Crypto;
using ZstdSharp;

namespace Solaris.Rpc.RpcModels;

public class SolanaAccount
{
    [JsonIgnore]
    public ReadOnlySpan<byte> DataSpan
    {
        get
        {
            switch (Data[1])
            {
                case "base64":
                    return Convert.FromBase64String(Data[0]);
                case "base64+zstd":
                    using (var decompressor = new Decompressor())
                        return decompressor.Unwrap(Convert.FromBase64String(Data[0]));
                default:
                    throw new NotSupportedException($"Account data encoding '{Data[1]}' is not supported.");
            }
        }
    }

    [JsonPropertyName("data")] public required string[] Data { get; set; }
    [JsonPropertyName("executable")] public bool Executable { get; set; }
    [JsonPropertyName("lamports")] public ulong Lamports { get; set; }
    [JsonPropertyName("owner")] public required string Owner { get; set; }
    [JsonPropertyName("rentEpoch")] public ulong RentEpoch { get; set; }
    [JsonPropertyName("space")] public ulong Space { get; set; }
}