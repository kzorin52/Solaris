using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Solaris.Base.Account;
using Solaris.Programs;
using Solaris.Transactions.LowLevel;
using Solaris.Transactions.Models;

namespace Solaris.Tests;

[TestClass]
public class TransactionDecodingTests
{
    private static PublicKey Key(byte value) => new(Enumerable.Repeat(value, 32).ToArray());
    private static PrivateKey Signer(byte value) => new(Enumerable.Repeat(value, 32).ToArray());

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void CompilerPacketsExposeBorrowedFieldsAndVerifiableSignatures(bool versioned)
    {
        var payer = Signer(1);
        var readOnlySigner = Signer(2);
        var writableSigner = Signer(3);
        var transaction = new Transaction
        {
            Version = versioned ? (byte)0 : null,
            BlockHash = Key(60), ExternalFeePayer = payer, Signers = [readOnlySigner, writableSigner],
            Instructions =
            [
                new TransactionInstruction
                {
                    ProgramId = Key(40), Data = [7, 8, 9],
                    Keys = [AccountMeta.ReadOnly(readOnlySigner, true), AccountMeta.Writable(writableSigner, true),
                        AccountMeta.Writable(Key(10)), AccountMeta.ReadOnly(Key(11))]
                },
                new TransactionInstruction { ProgramId = Key(41), Data = [], Keys = [] }
            ],
            AddressLookupTables = versioned ? [new(Key(50), [Key(11), Key(10)])] : []
        };
        var compiled = transaction.BuildMessage();
        var packet = transaction.Build();
        var decoded = DecodedTransaction.Parse(packet);
        Assert.AreEqual<ReadOnlyMemory<byte>>(packet, decoded.Data);
        Assert.AreEqual(3, decoded.SignatureCount);
        Assert.AreEqual(transaction.Version, decoded.Message.Version);
        Assert.AreEqual((byte)3, decoded.Message.Header.RequiredSignatures);
        Assert.AreEqual((byte)1, decoded.Message.Header.ReadOnlySignedAccounts);
        Assert.AreEqual(PublicKeyValue.Create(Key(60).KeySpan), decoded.Message.RecentBlockHash);
        Assert.AreEqual(compiled.Accounts.Length, decoded.Message.Accounts.Length);
        for (var i = 0; i < compiled.Accounts.Length; i++)
            Assert.AreEqual(PublicKeyValue.Create(compiled.Accounts[i].KeySpan), decoded.Message.Accounts[i]);
        for (var i = 0; i < decoded.SignatureCount; i++)
        {
            Assert.IsTrue(compiled.Accounts[i].Verify(decoded.Message.Data.Span, decoded.GetSignature(i)));
            Assert.IsTrue(packet.AsSpan(1 + 64 * i, 64).SequenceEqual(decoded.GetSignature(i)));
        }
        Assert.AreEqual<ReadOnlyMemory<byte>>(packet.AsMemory(193), decoded.Message.Data);
        var index = 0;
        foreach (var instruction in decoded.Message.Instructions)
        {
            Assert.AreEqual(compiled.Instructions[index].ProgramIdIndex, instruction.ProgramIdIndex);
            Assert.IsTrue(compiled.Instructions[index].KeyIndices.AsSpan().SequenceEqual(instruction.KeyIndices.Span));
            Assert.IsTrue(compiled.Instructions[index].Data.AsSpan().SequenceEqual(instruction.Data.Span));
            if (index == 0)
            {
                Assert.IsTrue(MemoryMarshal.TryGetArray(instruction.Data, out var storage));
                Assert.AreSame(packet, storage.Array);
            }
            index++;
        }
        Assert.AreEqual(2, index);
        Assert.AreEqual(2, decoded.Message.Instructions.Count);
        var lookupIndex = 0;
        foreach (var lookup in decoded.Message.AddressTableLookups)
        {
            Assert.AreEqual(PublicKeyValue.Create(Key(50).KeySpan), lookup.AccountKey);
            Assert.IsTrue(lookup.WritableIndices.Span.SequenceEqual(new byte[] { 1 }));
            Assert.IsTrue(lookup.ReadOnlyIndices.Span.SequenceEqual(new byte[] { 0 }));
            Assert.IsTrue(MemoryMarshal.TryGetArray(lookup.WritableIndices, out var storage));
            Assert.AreSame(packet, storage.Array);
            lookupIndex++;
        }
        Assert.AreEqual(versioned ? 1 : 0, lookupIndex);
        Assert.AreEqual(lookupIndex, decoded.Message.AddressTableLookups.Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => decoded.GetSignature(-1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => decoded.GetSignature(3));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void LiteralMessagesDecodeKnownFieldsAndRetainSlicedMemory(bool versioned)
    {
        var wire = CreateMessage(versioned ? (byte)0 : null,
            lookups: versioned ? [(new byte[] { 255 }, new byte[] { 0, 128 })] : null);
        var backing = new byte[wire.Length + 10];
        wire.CopyTo(backing, 4);
        ReadOnlyMemory<byte> memory = backing.AsMemory(4, wire.Length);
        var message = DecodedMessage.Parse(memory);
        Assert.AreEqual(memory, message.Data);
        Assert.AreEqual(versioned ? (byte?)0 : null, message.Version);
        Assert.AreEqual((byte)1, message.Header.RequiredSignatures);
        Assert.AreEqual((byte)0, message.Header.ReadOnlySignedAccounts);
        Assert.AreEqual((byte)1, message.Header.ReadOnlyUnsignedAccounts);
        Assert.AreEqual(2, message.Accounts.Length);
        Assert.AreEqual(PublicKeyValue.Create(Key(1).KeySpan), message.Accounts[0]);
        Assert.AreEqual(PublicKeyValue.Create(Key(2).KeySpan), message.Accounts[1]);
        Assert.AreEqual(PublicKeyValue.Create(Key(238).KeySpan), message.RecentBlockHash);
        var instructions = message.Instructions.GetEnumerator();
        Assert.IsTrue(instructions.MoveNext());
        Assert.AreEqual((byte)1, instructions.Current.ProgramIdIndex);
        Assert.IsTrue(instructions.Current.KeyIndices.Span.SequenceEqual(new byte[] { 0 }));
        Assert.IsTrue(instructions.Current.Data.Span.SequenceEqual(new byte[] { 171 }));
        Assert.IsFalse(instructions.MoveNext());
        Assert.IsFalse(instructions.MoveNext());
        var lookups = message.AddressTableLookups.GetEnumerator();
        Assert.AreEqual(versioned, lookups.MoveNext());
        if (versioned)
        {
            Assert.AreEqual(PublicKeyValue.Create(Key(90).KeySpan), lookups.Current.AccountKey);
            Assert.IsTrue(lookups.Current.WritableIndices.Span.SequenceEqual(new byte[] { 255 }));
            Assert.IsTrue(lookups.Current.ReadOnlyIndices.Span.SequenceEqual(new byte[] { 0, 128 }));
        }
        Assert.IsFalse(lookups.MoveNext());
    }

    [TestMethod]
    public void CompilerPacketSupportsLookupIndex255AndEveryAccountPosition()
    {
        var payer = Signer(1);
        var keys = Enumerable.Range(0, 253).Select(i => Key((byte)i)).Prepend(Key(255)).ToArray();
        var transaction = new Transaction
        {
            Version = 0, BlockHash = Key(60), Signers = [payer],
            Instructions = [new TransactionInstruction
            {
                ProgramId = Signer(3), Data = [], Keys = keys.Select(k => AccountMeta.Writable(k)).ToArray()
            }],
            AddressLookupTables = [new(Key(50), Enumerable.Range(0, 256).Select(i => Key((byte)i)).ToArray())]
        };
        var decoded = DecodedTransaction.Parse(transaction.Build());
        Assert.AreEqual(2, decoded.Message.Accounts.Length);
        foreach (var instruction in decoded.Message.Instructions)
        {
            Assert.AreEqual(254, instruction.KeyIndices.Length);
            for (var i = 0; i < 254; i++) Assert.AreEqual((byte)(i + 2), instruction.KeyIndices.Span[i]);
            Assert.IsTrue(instruction.Data.IsEmpty);
        }
        foreach (var lookup in decoded.Message.AddressTableLookups)
        {
            Assert.AreEqual(254, lookup.WritableIndices.Length);
            Assert.AreEqual((byte)255, lookup.WritableIndices.Span[0]);
            Assert.IsTrue(lookup.ReadOnlyIndices.IsEmpty);
        }
    }

    [TestMethod]
    [DataRow(127)]
    [DataRow(128)]
    [DataRow(16383)]
    [DataRow(16384)]
    [DataRow(65535)]
    public void BareMessageDataAcceptsCanonicalCompactU16Boundaries(int length)
    {
        var data = Enumerable.Range(0, length).Select(i => (byte)i).ToArray();
        var message = DecodedMessage.Parse(CreateMessage(instructions: [(1, [0], data)]));
        foreach (var instruction in message.Instructions) Assert.IsTrue(instruction.Data.Span.SequenceEqual(data));
    }

    [TestMethod]
    [DataRow(127)]
    [DataRow(128)]
    public void CompactU16BoundariesApplyToEveryCollectionLength(int count)
    {
        var keys = new byte[count];
        var instructions = Enumerable.Repeat(((byte)1, keys, Array.Empty<byte>()), count).ToArray();
        var message = DecodedMessage.Parse(CreateMessage(accountCount: count, instructions: instructions));
        Assert.AreEqual(count, message.Accounts.Length);
        Assert.AreEqual(count, message.Instructions.Count);
        var visited = 0;
        foreach (var instruction in message.Instructions)
        {
            Assert.AreEqual(count, instruction.KeyIndices.Length);
            visited++;
        }
        Assert.AreEqual(count, visited);
        var manyLookups = Enumerable.Repeat((new byte[] { 255 }, Array.Empty<byte>()), count).ToArray();
        message = DecodedMessage.Parse(CreateMessage(0, lookups: manyLookups));
        Assert.AreEqual(count, message.AddressTableLookups.Count);
        visited = 0;
        foreach (var lookup in message.AddressTableLookups)
        {
            Assert.AreEqual((byte)255, lookup.WritableIndices.Span[0]);
            visited++;
        }
        Assert.AreEqual(count, visited);
        message = DecodedMessage.Parse(CreateMessage(0, lookups: [(new byte[count], Array.Empty<byte>())]));
        foreach (var lookup in message.AddressTableLookups) Assert.AreEqual(count, lookup.WritableIndices.Length);
        message = DecodedMessage.Parse(CreateMessage(0, lookups: [(Array.Empty<byte>(), new byte[count])]));
        foreach (var lookup in message.AddressTableLookups) Assert.AreEqual(count, lookup.ReadOnlyIndices.Length);
    }

    [TestMethod]
    public void All256StaticAccountsAreValidForBareMessages()
    {
        var indices = Enumerable.Range(0, 256).Select(i => (byte)i).ToArray();
        var message = DecodedMessage.Parse(CreateMessage(accountCount: 256, instructions: [(255, indices, [])]));
        Assert.AreEqual(256, message.Accounts.Length);
        foreach (var instruction in message.Instructions)
        {
            Assert.AreEqual((byte)255, instruction.ProgramIdIndex);
            Assert.IsTrue(instruction.KeyIndices.Span.SequenceEqual(indices));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EveryTruncatedPrefixAndTrailingBytesAreRejected(bool versioned)
    {
        var message = CreateMessage(versioned ? (byte)0 : null,
            lookups: versioned ? [(new byte[] { 255 }, new byte[] { 128 })] : null);
        for (var length = 0; length < message.Length; length++)
        {
            var prefix = message.AsMemory(0, length);
            Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(prefix), $"Message prefix {length}");
        }
        var packet = Signed(message);
        for (var length = 0; length < packet.Length; length++)
        {
            var prefix = packet.AsMemory(0, length);
            Assert.ThrowsExactly<FormatException>(() => DecodedTransaction.Parse(prefix), $"Packet prefix {length}");
        }
        Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(message.Append((byte)0).ToArray()));
        Assert.ThrowsExactly<FormatException>(() => DecodedTransaction.Parse(packet.Append((byte)0).ToArray()));
    }

    [TestMethod]
    [DataRow(0, 0)]
    [DataRow(0, 3)]
    [DataRow(1, 1)]
    [DataRow(2, 2)]
    [DataRow(3, 0)]
    [DataRow(3, 3)]
    public void InvalidHeadersAndStaticAccountCountsAreRejected(int offset, int value)
    {
        var message = CreateMessage();
        message[offset] = (byte)value;
        Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(message));
        Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(CreateMessage(accountCount: 257)));
    }

    [TestMethod]
    public void InvalidInstructionIndicesAndLoadedAccountCountsAreRejected()
    {
        Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(CreateMessage(instructions: [(2, [0], [])])));
        Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(CreateMessage(instructions: [(1, [2], [])])));
        Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(CreateMessage(0, instructions: [(2, [0], [])], lookups: [(new byte[] { 0 }, Array.Empty<byte>())])));
        Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(CreateMessage(0, instructions: [(1, [3], [])], lookups: [(new byte[] { 0 }, Array.Empty<byte>())])));
        Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(CreateMessage(0, lookups: [(new byte[255], Array.Empty<byte>())])));
        // The last loaded read-only account is part of the same index space as writable accounts.
        var message = DecodedMessage.Parse(CreateMessage(0, instructions: [(1, [3], [])], lookups: [(new byte[] { 255 }, new byte[] { 254 })]));
        foreach (var instruction in message.Instructions) Assert.AreEqual((byte)3, instruction.KeyIndices.Span[0]);
    }

    [TestMethod]
    public void SignatureCountMustMatchHeaderAndRespectPacketLimit()
    {
        Assert.ThrowsExactly<FormatException>(() => DecodedTransaction.Parse(Signed(CreateMessage(), 0)));
        Assert.ThrowsExactly<FormatException>(() => DecodedTransaction.Parse(Signed(CreateMessage(), 2)));
        var maximum = Signed(CreateMessage(instructions: [(1, [0], new byte[1061])]));
        Assert.HasCount(1232, maximum);
        Assert.AreEqual(1232, DecodedTransaction.Parse(maximum).Data.Length);
        var oversizedMessage = CreateMessage(instructions: [(1, [0], new byte[1062])]);
        Assert.ThrowsExactly<FormatException>(() => DecodedTransaction.Parse(Signed(oversizedMessage)));
        Assert.AreEqual(oversizedMessage.Length, DecodedMessage.Parse(oversizedMessage).Data.Length);
        Assert.ThrowsExactly<FormatException>(() => DecodedTransaction.Parse(new byte[] { 255, 255, 3 }));
    }

    [TestMethod]
    public void NoncanonicalOverflowAndUnterminatedLengthsAreRejectedAtEveryLengthField()
    {
        byte[][] invalidLengths = [[128, 0], [129, 0], [128, 128, 0], [255, 255, 4], [255, 255, 128], [128, 128, 128, 0], [128]];
        var legacy = CreateMessage();
        // Static accounts, instructions, instruction keys, and instruction data.
        int[] legacyOffsets = [3, 100, 102, 104];
        var versioned = CreateMessage(0, lookups: [(new byte[] { 255 }, new byte[] { 0 })]);
        // Lookup count, writable count, and read-only count.
        int[] lookupOffsets = [107, 140, 142];
        foreach (var invalid in invalidLengths)
        {
            Assert.ThrowsExactly<FormatException>(() => DecodedTransaction.Parse(ReplaceLength(Signed(legacy), 0, invalid)));
            foreach (var offset in legacyOffsets)
                Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(ReplaceLength(legacy, offset, invalid)));
            foreach (var offset in lookupOffsets)
                Assert.ThrowsExactly<FormatException>(() => DecodedMessage.Parse(ReplaceLength(versioned, offset, invalid)));
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(127)]
    public void UnsupportedVersionsNeverFallBackToLegacy(int version)
    {
        var message = CreateMessage(0);
        message[0] = (byte)(128 | version);
        Assert.ThrowsExactly<NotSupportedException>(() => DecodedMessage.Parse(message));
        Assert.ThrowsExactly<NotSupportedException>(() => DecodedTransaction.Parse(Signed(message)));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void EmptyInstructionAndLookupCollectionsEnumerateWithoutElements(bool versioned)
    {
        var message = DecodedMessage.Parse(CreateMessage(versioned ? (byte)0 : null, instructions: []));
        Assert.AreEqual(0, message.Instructions.Count);
        Assert.IsFalse(message.Instructions.GetEnumerator().MoveNext());
        Assert.AreEqual(0, message.AddressTableLookups.Count);
        Assert.IsFalse(message.AddressTableLookups.GetEnumerator().MoveNext());
        Assert.IsFalse(default(DecodedInstructionCollection).GetEnumerator().MoveNext());
        Assert.IsFalse(default(DecodedAddressTableLookupCollection).GetEnumerator().MoveNext());
    }

    private static byte[] CreateMessage(byte? version = null, int accountCount = 2,
        (byte Program, byte[] Keys, byte[] Data)[]? instructions = null,
        (byte[] Writable, byte[] ReadOnly)[]? lookups = null)
    {
        var bytes = new List<byte>();
        if (version.HasValue) bytes.Add((byte)(128 | version.Value));
        bytes.AddRange([1, 0, (byte)(accountCount > 1 ? 1 : 0)]);
        AppendLength(bytes, accountCount);
        for (var i = 0; i < accountCount; i++) bytes.AddRange(Enumerable.Repeat((byte)(i + 1), 32));
        bytes.AddRange(Enumerable.Repeat((byte)238, 32));
        instructions ??= [(1, [0], [171])];
        AppendLength(bytes, instructions.Length);
        foreach (var instruction in instructions)
        {
            bytes.Add(instruction.Program);
            AppendLength(bytes, instruction.Keys.Length);
            bytes.AddRange(instruction.Keys);
            AppendLength(bytes, instruction.Data.Length);
            bytes.AddRange(instruction.Data);
        }
        if (version.HasValue)
        {
            lookups ??= [];
            AppendLength(bytes, lookups.Length);
            foreach (var lookup in lookups)
            {
                bytes.AddRange(Enumerable.Repeat((byte)90, 32));
                AppendLength(bytes, lookup.Writable.Length);
                bytes.AddRange(lookup.Writable);
                AppendLength(bytes, lookup.ReadOnly.Length);
                bytes.AddRange(lookup.ReadOnly);
            }
        }
        return bytes.ToArray();
    }

    private static byte[] Signed(byte[] message, int signatureCount = 1)
    {
        var bytes = new List<byte>();
        AppendLength(bytes, signatureCount);
        bytes.AddRange(new byte[signatureCount * 64]);
        bytes.AddRange(message);
        return bytes.ToArray();
    }

    private static byte[] ReplaceLength(byte[] data, int offset, byte[] replacement) =>
        [..data.AsSpan(0, offset), ..replacement, ..data.AsSpan(offset + 1)];

    private static void AppendLength(List<byte> bytes, int value)
    {
        do
        {
            var next = value >> 7;
            bytes.Add((byte)((value & 127) | (next == 0 ? 0 : 128)));
            value = next;
        } while (value != 0);
    }
}
