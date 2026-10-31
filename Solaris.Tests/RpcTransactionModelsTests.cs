using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Account;
using Solaris.Rpc.RpcModels;
using Solaris.Transactions.Models;

namespace Solaris.Tests;

[TestClass]
public class RpcTransactionModelsTests
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Base64PacketsExposeSharedBinaryViews(bool versioned)
    {
        var packet = BuildPacket(versioned ? (byte)0 : null);
        var result = Deserialize("{\"slot\":42,\"transaction\":" + Tuple(packet) + "}");
        Assert.AreEqual(42UL, result.Slot);
        CollectionAssert.AreEqual(packet, result.Transaction.Data.ToArray());
        Assert.AreEqual(1, result.Transaction.SignatureCount);
        CollectionAssert.AreEqual(packet.AsSpan(1, 64).ToArray(), result.Transaction.GetSignature(0).ToArray());
        var message = result.Transaction.Message;
        Assert.AreEqual(versioned ? (byte?)0 : null, message.Version);
        Assert.AreEqual(2, message.Accounts.Length);
        Assert.AreEqual(1, message.Instructions.Count);
        Assert.AreEqual(0, message.AddressTableLookups.Count);
        foreach (var instruction in message.Instructions)
        {
            Assert.AreEqual((byte)1, instruction.ProgramIdIndex);
            CollectionAssert.AreEqual(new byte[] { 0 }, instruction.KeyIndices.ToArray());
            CollectionAssert.AreEqual(new byte[] { 0, 1, 255 }, instruction.Data.ToArray());
        }
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"signatures\":[],\"message\":{\"accountKeys\":[],\"instructions\":[]}}")]
    [DataRow("{\"message\":{\"instructions\":[{\"parsed\":{\"type\":\"transfer\"}}]}}")]
    [DataRow("\"1LiA\"")]
    [DataRow("[\"1LiA\",\"base58\"]")]
    [DataRow("[\"1LiA\",\"binary\"]")]
    [DataRow("[\"AA==\",\"json\"]")]
    [DataRow("[\"AA==\",\"jsonParsed\"]")]
    [DataRow("[\"AA==\",\"base64+zstd\"]")]
    [DataRow("null")]
    [DataRow("[]")]
    [DataRow("[\"AA==\"]")]
    [DataRow("[\"AA==\",null]")]
    [DataRow("[0,\"base64\"]")]
    [DataRow("[\"AA==\",\"base64\",0]")]
    [DataRow("[\"not base64!\",\"base64\"]")]
    [DataRow("[\"\",\"base64\"]")]
    [DataRow("[\"AA==\",\"base64\"]")]
    public void UnsupportedRepresentationsAndMalformedTuplesAreRejected(string transaction)
    {
        Assert.ThrowsExactly<JsonException>(() => Deserialize("{\"transaction\":" + transaction + "}"));
    }

    [TestMethod]
    public void TruncatedAndTrailingPacketDataAreRejected()
    {
        var packet = BuildPacket();
        foreach (var malformed in new[] { packet[..64], packet[..^1], packet.Concat(new byte[] { 0 }).ToArray() })
            Assert.ThrowsExactly<JsonException>(() => Deserialize("{\"transaction\":" + Tuple(malformed) + "}"));
    }

    [TestMethod]
    public void UnsupportedBinaryVersionFailsExplicitly()
    {
        var packet = BuildPacket(0);
        packet[65] = 0x81;
        Assert.ThrowsExactly<NotSupportedException>(() => Deserialize("{\"transaction\":" + Tuple(packet) + "}"));
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"err\":null,\"innerInstructions\":null,\"logMessages\":null,\"preTokenBalances\":null,\"postTokenBalances\":null,\"rewards\":null,\"loadedAddresses\":null,\"returnData\":null,\"computeUnitsConsumed\":null,\"costUnits\":null,\"status\":null}")]
    public void UnavailableOptionalMetadataDoesNotBecomeEmptyRecordedResults(string metadata)
    {
        var result = WithTransaction(",\"meta\":" + metadata);
        var meta = result.Meta!;
        Assert.IsNull(result.BlockTime);
        Assert.IsNull(result.Version);
        Assert.IsNull(meta.Error);
        Assert.IsNull(meta.InnerInstructions);
        Assert.IsNull(meta.LogMessages);
        Assert.IsNull(meta.PreTokenBalances);
        Assert.IsNull(meta.PostTokenBalances);
        Assert.IsNull(meta.Rewards);
        Assert.IsNull(meta.LoadedAddresses);
        Assert.IsNull(meta.ReturnData);
        Assert.IsNull(meta.ComputeUnitsConsumed);
        Assert.IsNull(meta.CostUnits);
        Assert.IsNull(meta.Status);
    }

    [TestMethod]
    public void MissingAndNullTransactionMetadataRemainUnavailable()
    {
        Assert.IsNull(WithTransaction().Meta);
        var result = WithTransaction(",\"blockTime\":null,\"meta\":null");
        Assert.IsNull(result.Meta);
        Assert.IsNull(result.BlockTime);
        Assert.IsNull(JsonSerializer.Deserialize<HistoricalTransaction>("null"));
        Assert.ThrowsExactly<JsonException>(() => Deserialize("{\"slot\":1}"));
    }

    [TestMethod]
    public void LegacyNumericAndOmittedVersionsAreNotConflated()
    {
        var legacy = WithTransaction(",\"version\":\"legacy\"");
        Assert.IsTrue(legacy.Version!.Value.IsLegacy);
        Assert.IsNull(legacy.Version.Value.Number);
        var omitted = WithTransaction();
        Assert.IsNull(omitted.Version);
        Assert.IsNull(WithTransaction(",\"version\":null").Version);
        foreach (var number in new byte[] { 0, 1, 255 })
        {
            var numeric = WithTransaction(",\"version\":" + number);
            Assert.IsFalse(numeric.Version!.Value.IsLegacy);
            Assert.AreEqual((byte?)number, numeric.Version.Value.Number);
        }
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("256")]
    [DataRow("0.5")]
    [DataRow("\"0\"")]
    [DataRow("\"Legacy\"")]
    public void InvalidVersionsAreRejected(string version)
    {
        Assert.ThrowsExactly<JsonException>(() => WithTransaction(",\"version\":" + version));
    }

    [TestMethod]
    public void RawInnerInstructionsAndLoadedAddressesRemainAvailable()
    {
        var result = WithTransaction("""
            ,"meta":{"innerInstructions":[{"index":255,"instructions":[
              {"programIdIndex":255,"accounts":[0,255],"data":"abc","stackHeight":4294967295},
              {"programIdIndex":0,"accounts":[],"data":"","stackHeight":null}]}],
              "loadedAddresses":{"writable":["loaded-writable"],"readonly":["loaded-readonly"]}}
            """);
        var meta = result.Meta!;
        var inner = meta.InnerInstructions![0];
        Assert.AreEqual(255, inner.Index);
        Assert.AreEqual(255, inner.Instructions[0].ProgramIdIndex);
        CollectionAssert.AreEqual(new[] { 0, 255 }, inner.Instructions[0].Accounts);
        Assert.AreEqual("abc", inner.Instructions[0].Data);
        Assert.AreEqual((uint?)uint.MaxValue, inner.Instructions[0].StackHeight);
        Assert.IsNull(inner.Instructions[1].StackHeight);
        CollectionAssert.AreEqual(new[] { "loaded-writable" }, meta.LoadedAddresses!.Writable);
        CollectionAssert.AreEqual(new[] { "loaded-readonly" }, meta.LoadedAddresses.Readonly);
    }

    [TestMethod]
    [DataRow("{\"accounts\":[],\"data\":\"1\"}")]
    [DataRow("{\"program\":\"system\",\"parsed\":{\"type\":\"transfer\"}}")]
    [DataRow("{\"programId\":\"program\",\"accounts\":[\"key\"],\"data\":\"1\"}")]
    public void InnerInstructionsRequireRawProgramAndAccountIndices(string instruction)
    {
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<HistoricalInstruction>(instruction));
    }

    [TestMethod]
    public void MetadataPreservesSignedAndUnsignedBoundariesAndExactTokenAmounts()
    {
        var result = Deserialize($$$"""
            {
              "slot": 18446744073709551615, "blockTime": -9223372036854775808, "transaction": {{{Tuple(BuildPacket())}}},
              "meta": {
                "err": {"InstructionError": [0, {"Custom": 4294967295}]},
                "fee": 18446744073709551615,
                "preBalances": [0,18446744073709551615], "postBalances": [18446744073709551615,0],
                "innerInstructions": [], "logMessages": ["log"], "preTokenBalances": [],
                "postTokenBalances": [{"accountIndex": 255, "mint": "mint", "owner": "owner", "programId": "token-program",
                  "uiTokenAmount": {"amount": "18446744073709551616", "decimals": 255, "uiAmount": null, "uiAmountString": "184467440737095516.16"}},
                  {"accountIndex": 0, "mint": "old-mint",
                   "uiTokenAmount": {"amount": "125", "decimals": 2, "uiAmount": 1.25, "uiAmountString": "1.25"}}],
                "rewards": [
                  {"pubkey": "reward", "lamports": -9223372036854775808, "postBalance": 18446744073709551615, "rewardType": null, "commission": null},
                  {"pubkey": "voter", "lamports": 9223372036854775807, "postBalance": 0, "rewardType": "Voting", "commission": 100}
                ],
                "returnData": {"programId": "program", "data": ["AAEC/w==", "base64"]},
                "computeUnitsConsumed": 18446744073709551615, "costUnits": 18446744073709551615,
                "status": {"Err": {"InstructionError": [0, "InvalidArgument"]}}
              }
            }
            """);
        Assert.AreEqual(ulong.MaxValue, result.Slot);
        Assert.AreEqual(long.MinValue, result.BlockTime!.Value);
        var meta = result.Meta!;
        Assert.AreEqual(ulong.MaxValue, meta.Fee);
        CollectionAssert.AreEqual(new[] { 0UL, ulong.MaxValue }, meta.PreBalances);
        CollectionAssert.AreEqual(new[] { ulong.MaxValue, 0UL }, meta.PostBalances);
        Assert.AreEqual(uint.MaxValue, meta.Error!.Value.GetProperty("InstructionError")[1].GetProperty("Custom").GetUInt32());
        Assert.AreEqual("InvalidArgument", meta.Status!.Value.GetProperty("Err").GetProperty("InstructionError")[1].GetString());
        Assert.HasCount(0, meta.InnerInstructions!);
        Assert.HasCount(0, meta.PreTokenBalances!);
        Assert.AreEqual("log", meta.LogMessages![0]);
        var token = meta.PostTokenBalances![0];
        Assert.AreEqual(255, token.AccountIndex);
        Assert.AreEqual("mint", token.Mint);
        Assert.AreEqual("owner", token.Owner);
        Assert.AreEqual("token-program", token.ProgramId);
        Assert.AreEqual("18446744073709551616", token.UiTokenAmount.Amount);
        Assert.AreEqual("184467440737095516.16", token.UiTokenAmount.UiAmountString);
        Assert.AreEqual(byte.MaxValue, token.UiTokenAmount.Decimals);
        Assert.IsNull(token.UiTokenAmount.UiAmount);
        Assert.IsNull(meta.PostTokenBalances[1].Owner);
        Assert.IsNull(meta.PostTokenBalances[1].ProgramId);
        Assert.AreEqual((double?)1.25, meta.PostTokenBalances[1].UiTokenAmount.UiAmount);
        Assert.AreEqual(long.MinValue, meta.Rewards![0].Lamports);
        Assert.AreEqual(ulong.MaxValue, meta.Rewards[0].PostBalance);
        Assert.IsNull(meta.Rewards[0].Commission);
        Assert.IsNull(meta.Rewards[0].RewardType);
        Assert.AreEqual(long.MaxValue, meta.Rewards[1].Lamports);
        Assert.AreEqual("Voting", meta.Rewards[1].RewardType);
        Assert.AreEqual((byte?)100, meta.Rewards[1].Commission);
        Assert.AreEqual("program", meta.ReturnData!.ProgramId);
        CollectionAssert.AreEqual(new byte[] { 0, 1, 2, 255 }, meta.ReturnData.DataSpan.ToArray());
        Assert.AreEqual(ulong.MaxValue, meta.ComputeUnitsConsumed!.Value);
        Assert.AreEqual(ulong.MaxValue, meta.CostUnits!.Value);
    }

    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, 32).ToArray());

    private static byte[] BuildPacket(byte? version = null)
    {
        var payer = new PrivateKey(Enumerable.Repeat((byte)1, 32).ToArray());
        return new Transaction
        {
            Version = version,
            BlockHash = Key(60),
            Signers = [payer],
            Instructions = [new TransactionInstruction
            {
                ProgramId = Key(40), Keys = [AccountMeta.Writable(payer, true)], Data = [0, 1, 255]
            }]
        }.Build();
    }

    private static string Tuple(byte[] packet) => "[\"" + Convert.ToBase64String(packet) + "\",\"base64\"]";
    private static HistoricalTransaction WithTransaction(string properties = "") =>
        Deserialize("{\"slot\":1,\"transaction\":" + Tuple(BuildPacket()) + properties + "}");
    private static HistoricalTransaction Deserialize(string json) => JsonSerializer.Deserialize<HistoricalTransaction>(json)!;
}
