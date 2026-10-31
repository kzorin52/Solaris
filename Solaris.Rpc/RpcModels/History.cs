using System.Text.Json;
using System.Text.Json.Serialization;
using Solaris.Transactions.LowLevel;

namespace Solaris.Rpc.RpcModels;

public sealed class AddressSignature
{
    [JsonPropertyName("signature")] public required string Signature { get; set; }
    [JsonPropertyName("slot")] public ulong Slot { get; set; }
    [JsonPropertyName("err")] public JsonElement? Error { get; set; }
}

[JsonConverter(typeof(HistoricalTransactionConverter))]
public sealed class HistoricalTransaction
{
    public ulong Slot { get; set; }
    public long? BlockTime { get; set; }
    public TransactionVersion? Version { get; set; }
    public required DecodedTransaction Transaction { get; set; }
    public HistoricalTransactionMeta? Meta { get; set; }
}

[JsonConverter(typeof(TransactionVersionConverter))]
public readonly record struct TransactionVersion(byte? Number)
{
    public bool IsLegacy => Number is null;
    public static TransactionVersion Legacy => default;
}

public sealed class HistoricalInstruction
{
    [JsonPropertyName("programIdIndex")] public required int ProgramIdIndex { get; set; }
    [JsonPropertyName("accounts")] public required int[] Accounts { get; set; }
    [JsonPropertyName("data")] public required string Data { get; set; }
    [JsonPropertyName("stackHeight")] public uint? StackHeight { get; set; }
}

public sealed class HistoricalTransactionMeta
{
    [JsonPropertyName("err")] public JsonElement? Error { get; set; }
    [JsonPropertyName("fee")] public ulong Fee { get; set; }
    [JsonPropertyName("preBalances")] public ulong[] PreBalances { get; set; } = [];
    [JsonPropertyName("postBalances")] public ulong[] PostBalances { get; set; } = [];
    [JsonPropertyName("logMessages")] public string[]? LogMessages { get; set; }
    [JsonPropertyName("rewards")] public HistoricalReward[]? Rewards { get; set; }
    [JsonPropertyName("returnData")] public SimulateTransactionReturnData? ReturnData { get; set; }
    [JsonPropertyName("computeUnitsConsumed")] public ulong? ComputeUnitsConsumed { get; set; }
    [JsonPropertyName("costUnits")] public ulong? CostUnits { get; set; }
    [JsonPropertyName("status")] public JsonElement? Status { get; set; }
    [JsonPropertyName("loadedAddresses")] public LoadedTransactionAddresses? LoadedAddresses { get; set; }
    [JsonPropertyName("innerInstructions")] public HistoricalInnerInstructions[]? InnerInstructions { get; set; }
    [JsonPropertyName("preTokenBalances")] public HistoricalTokenBalance[]? PreTokenBalances { get; set; }
    [JsonPropertyName("postTokenBalances")] public HistoricalTokenBalance[]? PostTokenBalances { get; set; }
}

public sealed class LoadedTransactionAddresses
{
    [JsonPropertyName("writable")] public string[] Writable { get; set; } = [];
    [JsonPropertyName("readonly")] public string[] Readonly { get; set; } = [];
}

public sealed class HistoricalInnerInstructions
{
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("instructions")] public HistoricalInstruction[] Instructions { get; set; } = [];
}

public sealed class HistoricalTokenBalance
{
    [JsonPropertyName("accountIndex")] public int AccountIndex { get; set; }
    [JsonPropertyName("mint")] public required string Mint { get; set; }
    [JsonPropertyName("owner")] public string? Owner { get; set; }
    [JsonPropertyName("programId")] public string? ProgramId { get; set; }
    [JsonPropertyName("uiTokenAmount")] public required HistoricalTokenAmount UiTokenAmount { get; set; }
}

public sealed class HistoricalTokenAmount
{
    [JsonPropertyName("amount")] public required string Amount { get; set; }
    [JsonPropertyName("decimals")] public byte Decimals { get; set; }
    [JsonPropertyName("uiAmount")] public double? UiAmount { get; set; }
    [JsonPropertyName("uiAmountString")] public required string UiAmountString { get; set; }
}

public sealed class HistoricalReward
{
    [JsonPropertyName("pubkey")] public required string Pubkey { get; set; }
    [JsonPropertyName("lamports")] public long Lamports { get; set; }
    [JsonPropertyName("postBalance")] public ulong PostBalance { get; set; }
    [JsonPropertyName("rewardType")] public string? RewardType { get; set; }
    [JsonPropertyName("commission")] public byte? Commission { get; set; }
}

public sealed class LargestTokenAccount
{
    [JsonPropertyName("address")] public required string Address { get; set; }
    [JsonPropertyName("amount")] public required string Amount { get; set; }
    [JsonPropertyName("decimals")] public byte Decimals { get; set; }
}
