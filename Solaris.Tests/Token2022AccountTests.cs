using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Programs.Token2022;
using E = Solaris.Programs.Token2022.Token2022ExtensionType;

namespace Solaris.Tests;

[TestClass]
public class Token2022AccountTests
{
    private static PublicKey Key(byte n) => new(Enumerable.Repeat(n, 32).ToArray());

    [TestMethod]
    public void MintDecodeConsumesAbsentAuthorityPaddingAndRetainsIdentity()
    {
        var bytes = new byte[82];
        var serializer = new BorshSerializer(bytes);
        serializer.WriteInteger(0u); serializer.Write(Key(7).KeySpan);
        serializer.WriteInteger(ulong.MaxValue); serializer.Write((byte)9); serializer.Write(true);
        serializer.WriteInteger(1u); serializer.Write(Key(8).KeySpan);
        var mint = ((ReadOnlySpan<byte>)bytes).Deserialize<Token2022Mint>(Key(1));
        Assert.AreEqual(Key(1), mint.PublicKey);
        Assert.IsNull(mint.MintAuthority);
        Assert.AreEqual(ulong.MaxValue, mint.Supply);
        Assert.AreEqual(Key(8), mint.FreezeAuthority);
        Assert.IsTrue(mint.IsInitialized);
    }

    [TestMethod]
    public void AccountDecodeHandlesNativeReserveAndBothAuthorities()
    {
        var bytes = new byte[165];
        var serializer = new BorshSerializer(bytes);
        serializer.Write(Key(1).KeySpan); serializer.Write(Key(2).KeySpan); serializer.WriteInteger(1234UL);
        serializer.WriteInteger(1u); serializer.Write(Key(3).KeySpan); serializer.Write((byte)2);
        serializer.WriteInteger(1u); serializer.WriteInteger(ulong.MaxValue);
        serializer.WriteInteger(33UL); serializer.WriteInteger(1u); serializer.Write(Key(4).KeySpan);
        var account = ((ReadOnlySpan<byte>)bytes).Deserialize<Token2022Account>(Key(50));
        Assert.AreEqual(Key(1), account.Mint); Assert.AreEqual(Key(2), account.Owner);
        Assert.AreEqual(1234UL, account.Amount); Assert.AreEqual(Key(3), account.Delegate);
        Assert.AreEqual(Token2022AccountState.Frozen, account.State);
        Assert.AreEqual(ulong.MaxValue, account.NativeReserve);
        Assert.AreEqual(33UL, account.DelegatedAmount); Assert.AreEqual(Key(4), account.CloseAuthority);
    }

    [TestMethod]
    public void TlvRejectsDuplicatesTruncationAndWrongAccountTypeAndPreservesUnknownEntries()
    {
        var bytes = new byte[12];
        var serializer = new BorshSerializer(bytes);
        serializer.WriteInteger((ushort)E.TransferFeeAmount); serializer.WriteInteger((ushort)8); serializer.WriteInteger(ulong.MaxValue);
        var extensions = Token2022Extensions.CopyFrom(bytes, Token2022AccountType.Account);
        Assert.AreEqual(ulong.MaxValue, extensions.Get<Token2022TransferFeeAmount>().WithheldAmount);
        Assert.ThrowsExactly<InvalidDataException>(() => Token2022Extensions.CopyFrom(bytes, Token2022AccountType.Mint));
        Assert.ThrowsExactly<InvalidDataException>(() => Token2022Extensions.CopyFrom(bytes.AsSpan(0, 11), Token2022AccountType.Account));
        var duplicate = bytes.Concat(bytes).ToArray();
        Assert.ThrowsExactly<InvalidDataException>(() => Token2022Extensions.CopyFrom(duplicate, Token2022AccountType.Account));
        var unknown = new byte[] { 0xFE, 0xFF, 3, 0, 7, 8, 9 };
        var forward = Token2022Extensions.CopyFrom(unknown, Token2022AccountType.Account);
        Assert.IsTrue(forward.TryGetRaw(65534, out var raw));
        CollectionAssert.AreEqual(new byte[] { 7, 8, 9 }, raw.ToArray());
        bytes[4] = 0;
        Assert.AreEqual(ulong.MaxValue, extensions.Get<Token2022TransferFeeAmount>().WithheldAmount);
    }

    [TestMethod]
    public void ExtensionSizingDeduplicatesAndAvoidsMultisigLengthCollision()
    {
        Assert.AreEqual(82, Token2022ExtensionSizes.GetMintSize(ReadOnlySpan<E>.Empty));
        Assert.AreEqual(165, Token2022ExtensionSizes.GetAccountSize(ReadOnlySpan<E>.Empty));
        Assert.AreEqual(170, Token2022ExtensionSizes.GetAccountSize(new E[] { E.ImmutableOwner, E.ImmutableOwner }));
        Assert.AreEqual(357, Token2022ExtensionSizes.GetMintSize(new Token2022ExtensionSize[] { new(E.TokenMetadata, 185) }));
        Assert.ThrowsExactly<ArgumentException>(() => Token2022ExtensionSizes.GetAccountSize(new E[] { E.TransferFeeConfig }));
        Span<E> required = stackalloc E[5];
        var count = Token2022ExtensionSizes.GetRequiredAccountExtensions([E.TransferFeeConfig, E.NonTransferable, E.TransferHook, E.Pausable, E.TransferFeeConfig], required);
        CollectionAssert.AreEquivalent(new E[] { E.TransferFeeAmount, E.NonTransferableAccount, E.ImmutableOwner, E.TransferHookAccount, E.PausableAccount }, required[..count].ToArray());
    }

    [TestMethod]
    public void FeeCalculationRoundsUpCapsAndHandlesUlongBoundaries()
    {
        var fee = new Token2022TransferFee(10, 100, 250);
        Assert.AreEqual(1UL, fee.CalculateFee(1));
        Assert.AreEqual(25UL, fee.CalculateFee(1000));
        Assert.AreEqual(100UL, fee.CalculateFee(ulong.MaxValue));
        Assert.AreEqual(1000UL, fee.CalculatePreFeeAmount(975));
        Assert.AreEqual(25UL, fee.CalculateInverseFee(975));
        var full = new Token2022TransferFee(0, ulong.MaxValue, 10000);
        Assert.AreEqual(ulong.MaxValue, full.CalculateFee(ulong.MaxValue));
        Assert.IsNull(full.CalculatePreFeeAmount(1));
        var maxRate = new Token2022TransferFee(0, 10, 10000);
        Assert.AreEqual(11UL, maxRate.CalculatePreFeeAmount(1));
        Assert.IsNull(maxRate.CalculatePreFeeAmount(ulong.MaxValue));
        Assert.AreEqual(0UL, full.CalculatePreFeeAmount(0));
        for (ulong amount = 1; amount < 10000; amount += 97)
        {
            var post = fee.CalculatePostFeeAmount(amount);
            var pre = fee.CalculatePreFeeAmount(post);
            Assert.IsTrue(pre.HasValue);
            Assert.AreEqual(post, fee.CalculatePostFeeAmount(pre.Value));
        }
        var config = new Token2022TransferFeeConfig(null, null, 0, new(0, 100, 100), new(10, 100, 200));
        Assert.AreEqual(10UL, config.CalculateEpochFee(9, 1000));
        Assert.AreEqual(20UL, config.CalculateEpochFee(10, 1000));
    }
}
