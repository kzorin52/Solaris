using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Account;
using Solaris.Programs;
using Solaris.Transactions.LowLevel;
using Solaris.Transactions.Models;

[assembly: Parallelize(Scope = ExecutionScope.MethodLevel)]

namespace Solaris.Tests;

[TestClass]
public class TransactionTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, 32).ToArray());
    private static PrivateKey Signer(byte value) => new(Enumerable.Repeat(value, 32).ToArray());

    [TestMethod]
    public void LookupsKeepSignersAndProgramsStaticAndUseFirstTable()
    {
        var payer = Signer(1);
        var signer = Signer(2);
        var transaction = new Transaction
        {
            Version = 0, BlockHash = Key(60), ExternalFeePayer = payer, Signers = [signer],
            Instructions = [new TransactionInstruction
            {
                ProgramId = Key(40), Data = [],
                Keys = [AccountMeta.ReadOnly(signer, true), AccountMeta.ReadOnly(Key(11)), AccountMeta.Writable(Key(10)), AccountMeta.Writable(Key(11))]
            }],
            AddressLookupTables = [new(Key(50), [Key(11), signer, Key(40)]), new(Key(51), [Key(11), Key(10)])]
        };
        var message = transaction.BuildMessage();
        CollectionAssert.AreEqual(new[] { payer.PublicKey, signer.PublicKey, Key(40) }, message.Accounts);
        CollectionAssert.AreEqual(new byte[] { 0 }, message.AddressTableLookups![0].WritableIndices);
        CollectionAssert.AreEqual(new byte[] { 1 }, message.AddressTableLookups[1].WritableIndices);
        CollectionAssert.AreEqual(new byte[] { 1, 3, 4, 3 }, message.Instructions[0].KeyIndices);
        var before = transaction.Signers;
        var packet = transaction.Build();
        Assert.AreEqual("F86EB942E1B792D92DDB745D4FBB55F17FCF03A001DD43699EB0EDF05A9A149A", Convert.ToHexString(SHA256.HashData(packet)));
        Assert.AreSame(before, transaction.Signers);
        var bytes = new byte[message.Size()];
        message.Serialize(bytes);
        Assert.IsTrue(payer.PublicKey.Verify(bytes, packet.AsSpan(1, 64)));
        Assert.IsTrue(signer.PublicKey.Verify(bytes, packet.AsSpan(65, 64)));
    }

    [TestMethod]
    public void All256AccountIndicesAndLookupIndex255AreRepresentable()
    {
        var payer = Signer(1);
        var keys = Enumerable.Range(0, 253).Select(i => Key((byte)i)).Prepend(Key(255)).ToArray();
        var transaction = new Transaction
        {
            Version = 0, BlockHash = Key(60), Signers = [payer],
            Instructions = [new TransactionInstruction { ProgramId = Signer(3), Data = [], Keys = keys.Select(k => AccountMeta.Writable(k)).ToArray() }],
            AddressLookupTables = [new(Key(50), Enumerable.Range(0, 256).Select(i => Key((byte)i)).ToArray())]
        };
        var message = transaction.BuildMessage();
        Assert.AreEqual((byte)255, message.AddressTableLookups![0].WritableIndices[0]);
        Assert.AreEqual((byte)255, message.Instructions[0].KeyIndices[^1]);
        Assert.IsLessThanOrEqualTo(1232, transaction.Build().Length);
        transaction.Instructions[0].Keys = [..transaction.Instructions[0].Keys, AccountMeta.Writable(Key(254))];
        Assert.ThrowsExactly<InvalidOperationException>(() => transaction.BuildMessage());
    }

    [TestMethod]
    public void SignerArrayOrderDoesNotDetermineSignatureOrder()
    {
        var payer = Signer(1); var first = Signer(2); var second = Signer(3);
        var transaction = new Transaction
        {
            Version = 0, BlockHash = Key(60), ExternalFeePayer = payer, Signers = [second, first],
            Instructions = [new TransactionInstruction { ProgramId = Key(40), Data = [], Keys = [AccountMeta.ReadOnly(first, true), AccountMeta.ReadOnly(second, true)] }]
        };
        var message = transaction.BuildMessage();
        var bytes = new byte[message.Size()]; message.Serialize(bytes);
        var packet = transaction.Build();
        for (var i = 0; i < 3; i++) Assert.IsTrue(message.Accounts[i].Verify(bytes, packet.AsSpan(1 + 64 * i, 64)));
        transaction.Signers = [first];
        Assert.ThrowsExactly<InvalidOperationException>(() => transaction.Build());
    }

    [TestMethod]
    [DataRow(127, 1)]
    [DataRow(128, 2)]
    [DataRow(255, 2)]
    [DataRow(256, 2)]
    public void InstructionDataUsesShortvec(int length, int prefixLength)
    {
        var message = new Transaction { BlockHash = Key(60), Signers = [Signer(1)], Instructions = [new TransactionInstruction { ProgramId = Key(40), Keys = [], Data = new byte[length] }] }.BuildMessage();
        var bytes = new byte[message.Size()]; message.Serialize(bytes);
        const int offset = 3 + 1 + 64 + 32 + 1 + 1 + 1;
        Assert.HasCount(offset + prefixLength + length, bytes);
        Assert.AreEqual((byte)((length & 127) | (prefixLength > 1 ? 128 : 0)), bytes[offset]);
        if (prefixLength == 2) Assert.AreEqual((byte)(length >> 7), bytes[offset + 1]);
    }

    [TestMethod]
    public void StaticAccountAndInstructionCountsUseShortvec()
    {
        var message = new CompiledMessage
        {
            Header = new CompiledMessageHeader { RequiredSignatures = 1, ReadOnlyUnsignedAccounts = 127 },
            Accounts = Enumerable.Range(0, 128).Select(i => Key((byte)i)).ToArray(), RecentBlockHash = Key(60),
            Instructions = Enumerable.Range(0, 128).Select(_ => new CompiledInstruction { ProgramIdIndex = 1, KeyIndices = [], Data = [] }).ToArray()
        };
        var bytes = new byte[message.Size()]; message.Serialize(bytes);
        Assert.AreEqual((byte)128, bytes[3]); Assert.AreEqual((byte)1, bytes[4]);
        const int offset = 3 + 2 + 128 * 32 + 32;
        Assert.AreEqual((byte)128, bytes[offset]); Assert.AreEqual((byte)1, bytes[offset + 1]);
    }

    [TestMethod]
    public void UnsupportedVersionsLegacyLookupsAndOversizePacketsFail()
    {
        var transaction = new Transaction { Version = 1, BlockHash = Key(60), Signers = [Signer(1)], Instructions = [] };
        Assert.ThrowsExactly<NotSupportedException>(() => transaction.BuildMessage());
        transaction.Version = null; transaction.AddressLookupTables = [new(Key(50), [Key(10)])];
        Assert.ThrowsExactly<InvalidOperationException>(() => transaction.BuildMessage());
        transaction.Version = 0; transaction.AddressLookupTables = [];
        transaction.Instructions = [new TransactionInstruction { ProgramId = Key(40), Keys = [], Data = new byte[1232] }];
        Assert.ThrowsExactly<InvalidOperationException>(() => transaction.Build());
    }

    [TestMethod]
    public void LookupStateRejectsMalformedAccountsAndPreservesFrozenMetadata()
    {
        foreach (var data in new[] { new byte[55], new byte[57], new byte[56], new byte[56 + 257 * 32] })
            Assert.ThrowsExactly<ArgumentException>(() => Parse(data));
        var valid = new byte[88]; valid[0] = 1; valid[4] = 42; valid[12] = 12;
        Key(10).KeySpan.CopyTo(valid.AsSpan(56));
        var state = new AddressLookupTableState(valid);
        Assert.IsNull(state.Authority); Assert.IsFalse(state.IsActive);
        Assert.AreEqual(42UL, state.DeactivationSlot); Assert.AreEqual(12UL, state.LastExtendedSlot);
        Assert.AreEqual(Key(10).KeyValue, state.GetAddress(0));
        valid[21] = 2;
        Assert.ThrowsExactly<ArgumentException>(() => Parse(valid));
    }

    private static void Parse(byte[] data) { _ = new AddressLookupTableState(data); }
}
