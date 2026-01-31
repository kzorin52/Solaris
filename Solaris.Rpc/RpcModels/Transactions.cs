using System.Text.Json.Serialization;

namespace Solaris.Rpc.RpcModels;

public class SimulateTransactionConfig
{
    [JsonPropertyName("commitment")] public string Commitment { get; set; } = "finalized";

    [JsonPropertyName("encoding")] public string Encoding { get; set; } = "base64";

    [JsonPropertyName("replaceRecentBlockhash")]
    public bool ReplaceRecentBlockhash { get; set; } = true;

    [JsonPropertyName("sigVerify")] public bool SigVerify { get; set; } = false;

    [JsonPropertyName("innerInstructions")]
    public bool InnerInstructions { get; set; } = false;

    [JsonPropertyName("accounts")] public SimulateTransactionAccountsConfig? Accounts { get; set; }
}

public class SimulateTransactionAccountsConfig
{
    [JsonPropertyName("addresses")] public required string[] Addresses { get; set; }

    [JsonPropertyName("encoding")] public string Encoding { get; set; } = "base64";
}

public class SimulateTransactionResult
{
    [JsonPropertyName("accounts")] public SolanaAccount?[]? Accounts { get; set; }

    [JsonPropertyName("err")] public object? Err { get; set; }

    [JsonPropertyName("innerInstructions")]
    public object? InnerInstructions { get; set; }

    [JsonPropertyName("logs")] public string[]? Logs { get; set; }

    [JsonPropertyName("replacementBlockhash")]
    public LatestBlockhash? ReplacementBlockhash { get; set; }

    [JsonPropertyName("returnData")] public SimulateTransactionReturnData? ReturnData { get; set; }

    [JsonPropertyName("unitsConsumed")] public ulong? UnitsConsumed { get; set; }
}

public class SimulateTransactionReturnData
{
    [JsonIgnore] public ReadOnlySpan<byte> DataSpan => Convert.FromBase64String(Data[0]);

    [JsonPropertyName("programId")] public required string ProgramId { get; set; }

    [JsonPropertyName("data")] public required string[] Data { get; set; }
}