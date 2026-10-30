using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Programs;
using Solaris.Programs.Token2022;
using Solaris.Transactions.Models;
using T = Solaris.Programs.Token2022.Token2022Program;

namespace Solaris.Tests;

[TestClass]
public class Token2022Tests
{
    private static PublicKey Key(byte n) => new(Enumerable.Repeat(n, 32).ToArray());

    [TestMethod]
    public void Token2022AssociatedAccountsUseDistinctSeedsAndProgram()
    {
        var owner = Key(1); var mint = Key(2);
        var legacy = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, mint, out _);
        var token2022 = AssociatedTokenAccountProgram.DeriveAssociatedTokenAccount(owner, mint, out _, T.ProgramId);
        Assert.AreNotEqual(legacy, token2022);
        var instruction = AssociatedTokenAccountProgram.CreateAssociatedTokenAccount(Key(3), owner, mint, token2022, true, T.ProgramId);
        Assert.AreEqual(token2022, instruction.Keys[1].Account);
        Assert.AreEqual(T.ProgramId, instruction.Keys[5].Account);
    }

    [TestMethod]
    public void MultisigAuthorityAndHookAccountsRetainDistinctPrivileges()
    {
        var remaining = new[] { AccountMeta.Writable(Key(8)), AccountMeta.ReadOnly(Key(9)) };
        var instruction = T.TransferChecked(Key(1), Key(2), Key(3), ulong.MaxValue, 9, Key(4), [Key(5), Key(6)], remaining);
        Assert.IsFalse(instruction.Keys[3].IsSigner);
        Assert.IsTrue(instruction.Keys[4].IsSigner);
        Assert.IsTrue(instruction.Keys[5].IsSigner);
        Assert.IsFalse(instruction.Keys[4].IsWritable);
        Assert.AreSame(remaining[0], instruction.Keys[6]);
        Assert.AreSame(remaining[1], instruction.Keys[7]);
        var single = T.TransferChecked(Key(1), Key(2), Key(3), 1, 9, Key(4));
        Assert.IsTrue(single.Keys[3].IsSigner);
    }

    [TestMethod]
    public void BatchPreservesDuplicateAccountsAndFramesEachInstruction()
    {
        var first = T.Transfer(Key(1), Key(2), 17, Key(3));
        var second = T.CloseAccount(Key(1), Key(2), Key(3));
        var batch = T.Batch([first, second]);
        var expected = new byte[] { 255, 3, 9, 3, 17, 0, 0, 0, 0, 0, 0, 0, 3, 1, 9 };
        CollectionAssert.AreEqual(expected, batch.Data);
        CollectionAssert.AreEqual(first.Keys.Concat(second.Keys).ToArray(), batch.Keys);
        Assert.ThrowsExactly<ArgumentException>(() => T.Batch([batch]));
        Assert.ThrowsExactly<ArgumentException>(() => T.Batch([SystemProgram.Transfer(Key(1), Key(2), 1)]));
    }

    [TestMethod]
    public void HookResolverSupportsLiteralAndAllSeedSourcesAndDeescalatesDuplicates()
    {
        var program = Key(60); var mint = Key(2);
        var validationAddress = TransferHookExtraAccounts.DeriveValidationAccount(mint, program, out _);
        var authorityData = Enumerable.Range(0, 64).Select(i => (byte)i).ToArray();
        var seedConfig = new byte[32];
        var serializer = new BorshSerializer(seedConfig);
        serializer.Write((byte)1); serializer.Write((byte)2); serializer.Write(new byte[] { 10, 11 });
        serializer.Write((byte)2); serializer.Write((byte)8); serializer.Write((byte)8);
        serializer.Write((byte)3); serializer.Write((byte)0);
        serializer.Write((byte)4); serializer.Write((byte)3); serializer.Write((byte)4); serializer.Write((byte)3);
        var validation = new TransferHookExtraAccountMetaList
        {
            PublicKey = validationAddress,
            Accounts = [new(0, Key(1).KeyValue, true, true), new(1, PublicKeyValue.Create(seedConfig), false, true)]
        };
        var result = TransferHookExtraAccounts.Resolve(program, Key(1), mint, Key(3), Key(4), 0x0807060504030201, validation, k =>
        {
            Assert.AreEqual(Key(4), k);
            return authorityData;
        });
        Assert.AreEqual(Key(1), result[0].Account);
        Assert.IsFalse(result[0].IsSigner);
        Assert.IsFalse(result[0].IsWritable);
        var seeds = new byte[45];
        seeds[0] = 10; seeds[1] = 11;
        for (var i = 0; i < 8; i++) seeds[2 + i] = (byte)(i + 1);
        Key(1).KeySpan.CopyTo(seeds.AsSpan(10));
        authorityData.AsSpan(4, 3).CopyTo(seeds.AsSpan(42));
        var expected = PublicKey.FindProgramAddress(program, seeds).Key;
        Assert.AreEqual(expected, result[1].Account);
        Assert.IsTrue(result[1].IsWritable);
        Assert.AreEqual(program, result[^2].Account);
        Assert.AreEqual(validationAddress, result[^1].Account);
    }

    [TestMethod]
    public void HookResolverReadsPubkeysAndExternalProgramsFromEarlierAccounts()
    {
        var accountData = new byte[64]; Key(19).KeySpan.CopyTo(accountData.AsSpan(7));
        var pubkeyConfig = new byte[32]; pubkeyConfig[0] = 2; pubkeyConfig[1] = 3; pubkeyConfig[2] = 7;
        var validation = new TransferHookExtraAccountMetaList
        {
            Accounts = [new(0, Key(55).KeyValue, false, false), new(133, PublicKeyValue.Create(new byte[32]), false, false), new(2, PublicKeyValue.Create(pubkeyConfig), false, false)]
        };
        var result = TransferHookExtraAccounts.Resolve(Key(60), Key(1), Key(2), Key(3), Key(4), 1, validation, _ => accountData);
        Assert.AreEqual(PublicKey.FindProgramAddress(Key(55), ReadOnlySpan<byte>.Empty).Key, result[1].Account);
        Assert.AreEqual(Key(19), result[2].Account);
        Assert.ThrowsExactly<InvalidOperationException>(() => TransferHookExtraAccounts.Resolve(Key(60), Key(1), Key(2), Key(3), Key(4), 1, validation));
    }

    [TestMethod]
    public void HookValidationAccountRejectsTruncationAndInvalidFlags()
    {
        var bytes = new byte[51];
        var serializer = new BorshSerializer(bytes);
        serializer.Write(new byte[] { 105, 37, 101, 197, 75, 251, 102, 26 });
        serializer.WriteInteger(39u); serializer.WriteInteger(1u);
        serializer.Write((byte)0); serializer.Write(Key(12).KeySpan); serializer.Write(false); serializer.Write(true);
        var value = ((ReadOnlySpan<byte>)bytes).Deserialize<TransferHookExtraAccountMetaList>(Key(40));
        Assert.AreEqual(Key(40), value.PublicKey);
        Assert.AreEqual(Key(12).KeyValue, value.Accounts[0].AddressConfig);
        Assert.IsTrue(value.Accounts[0].IsWritable);
        bytes[^1] = 2;
        Assert.ThrowsExactly<ArgumentException>(() => ((ReadOnlySpan<byte>)bytes).Deserialize<TransferHookExtraAccountMetaList>());
        Assert.ThrowsExactly<ArgumentException>(() => ((ReadOnlySpan<byte>)bytes.AsSpan(0, 50)).Deserialize<TransferHookExtraAccountMetaList>());
    }
}
