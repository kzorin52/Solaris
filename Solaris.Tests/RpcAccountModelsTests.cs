using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Crypto;
using Solaris.Rpc.RpcModels;
using ZstdSharp;

namespace Solaris.Tests;

[TestClass]
public class RpcAccountModelsTests
{
    private static string AccountJson(string data) => $$"""
        {"data":{{data}},"executable":true,"lamports":18446744073709551615,"owner":"owner","rentEpoch":18446744073709551615,"space":18446744073709551615}
        """;

    private static SolanaAccount Account(string data) =>
        JsonSerializer.Deserialize<SolanaAccount>(AccountJson(data))!;

    [TestMethod]
    [DataRow("base64")]
    [DataRow("base58")]
    public void EncodedDataDecodesLeadingZeroAndFullByteRange(string encoding)
    {
        var bytes = new byte[] { 0, 0 }.Concat(Enumerable.Range(0, 256).Select(value => (byte)value)).ToArray();
        var encoded = encoding == "base64" ? Convert.ToBase64String(bytes) : Base58.EncodeData(bytes);
        var account = Account(JsonSerializer.Serialize(new[] { encoded, encoding }));

        Assert.AreEqual(encoded, account.Data[0]);
        Assert.AreEqual(encoding, account.Data[1]);
        CollectionAssert.AreEqual(bytes, account.DataSpan.ToArray());
        CollectionAssert.AreEqual(bytes, JsonSerializer.Deserialize<SolanaAccount>(JsonSerializer.Serialize(account))!.DataSpan.ToArray());
    }

    [TestMethod]
    [DataRow("base64")]
    [DataRow("base58")]
    public void EmptyEncodedPayloadDecodesToEmptyBytes(string encoding)
    {
        Assert.AreEqual(0, Account(JsonSerializer.Serialize(new[] { "", encoding })).DataSpan.Length);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(256)]
    [DataRow(4096)]
    public void ZstdPayloadDecodesAfterBase64(int length)
    {
        var bytes = Enumerable.Range(0, length).Select(value => (byte)value).ToArray();
        using var compressor = new Compressor(3);
        var compressed = compressor.Wrap(bytes);
        var account = Account(JsonSerializer.Serialize(new[] { Convert.ToBase64String(compressed), "base64+zstd" }));

        CollectionAssert.AreEqual(bytes, account.DataSpan.ToArray());
    }

    [TestMethod]
    public void ZstdFrameWithoutDeclaredContentSizeDecodes()
    {
        // Zstd magic, unknown-size frame header, final four-byte raw block.
        byte[] frame = [0x28, 0xB5, 0x2F, 0xFD, 0x00, 0x00, 0x21, 0x00, 0x00, 0x00, 0x00, 0x01, 0xFF];
        var account = Account(JsonSerializer.Serialize(new[] { Convert.ToBase64String(frame), "base64+zstd" }));

        CollectionAssert.AreEqual(new byte[] { 0, 0, 1, 255 }, account.DataSpan.ToArray());
    }

    [TestMethod]
    public void AccountMetadataRetainsUnsigned64BitBoundaries()
    {
        var account = Account("[\"\",\"base64\"]");
        Assert.IsTrue(account.Executable);
        Assert.AreEqual("owner", account.Owner);
        Assert.AreEqual(ulong.MaxValue, account.Lamports);
        Assert.AreEqual(ulong.MaxValue, account.RentEpoch);
        Assert.AreEqual(ulong.MaxValue, account.Space);

        var zero = JsonSerializer.Deserialize<SolanaAccount>(AccountJson("[\"\",\"base64\"]")
            .Replace("18446744073709551615", "0"))!;
        Assert.AreEqual(0UL, zero.Lamports);
        Assert.AreEqual(0UL, zero.RentEpoch);
        Assert.AreEqual(0UL, zero.Space);
    }

    [TestMethod]
    [DataRow("\"112\"")]
    [DataRow("42")]
    [DataRow("{\"program\":\"spl-token\",\"parsed\":{\"type\":\"mint\"},\"space\":82}")]
    public void UnsupportedAccountDataShapesAreRejected(string json)
    {
        Assert.ThrowsExactly<JsonException>(() => Account(json));
    }

    [TestMethod]
    [DataRow("binary")]
    [DataRow("future-encoding")]
    public void UnsupportedAccountEncodingsCannotBeDecoded(string encoding)
    {
        var account = Account(JsonSerializer.Serialize(new[] { "payload", encoding }));
        Assert.ThrowsExactly<NotSupportedException>(() => account.DataSpan.ToArray());
    }
}
