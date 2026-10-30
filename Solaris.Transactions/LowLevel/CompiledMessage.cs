using System.Runtime.InteropServices;
using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Transactions.LowLevel;

public struct CompiledMessage
{
    public byte? Version;
    public CompiledMessageHeader Header;
    public PublicKey[] Accounts;
    public PublicKey RecentBlockHash;
    public CompiledInstruction[] Instructions;
    public MessageAddressTableLookup[]? AddressTableLookups;

    public byte[] BuildTransaction(PrivateKey[] keys, PrivateKey? feePayer = null)
    {
        var size = TransactionSize();
        if (size > 1232) throw new InvalidOperationException("Transaction exceeds the 1232-byte packet limit.");
        var buffer = new byte[size];
        SerializeTransactionCore(buffer, keys, feePayer, size);
        return buffer;
    }

    public int SerializeTransaction(Span<byte> buffer, ReadOnlySpan<PrivateKey> keys, PrivateKey? feePayer = null)
    {
        var size = TransactionSize();
        if (size > 1232) throw new InvalidOperationException("Transaction exceeds the 1232-byte packet limit.");
        if (buffer.Length < size) throw new ArgumentException("Transaction buffer is too small.", nameof(buffer));
        SerializeTransactionCore(buffer, keys, feePayer, size);
        return size;
    }

    private void SerializeTransactionCore(Span<byte> buffer, ReadOnlySpan<PrivateKey> keys, PrivateKey? feePayer, int size)
    {
        var signatureCount = Header.RequiredSignatures;
        var prefix = new BorshSerializer(buffer);
        WriteLength(ref prefix, signatureCount);
        var messageOffset = prefix.Offset + 64 * signatureCount;
        var message = buffer.Slice(messageOffset, size - messageOffset);
        SerializeCore(message);
        for (var i = 0; i < signatureCount; i++)
        {
            PrivateKey? signer = feePayer?.PublicKey == Accounts[i] ? feePayer : null;
            if (signer is null)
                foreach (var key in keys)
                    if (key.PublicKey == Accounts[i])
                    {
                        signer = key;
                        break;
                    }
            if (signer is null) throw new InvalidOperationException($"Missing signer for {Accounts[i]}.");
            signer.Sign(message, buffer.Slice(prefix.Offset + i * 64, 64));
        }
    }

    public int TransactionSize() => LengthSize(Header.RequiredSignatures) + Header.RequiredSignatures * 64 + Size();

    public int Size()
    {
        if (Version is not null and not 0) throw new NotSupportedException($"Transaction version {Version} is not supported.");
        if (Version is null && AddressTableLookups is { Length: > 0 })
            throw new InvalidOperationException("Address lookups require a version 0 message.");
        if (Accounts.Length > 256 || Header.RequiredSignatures > 127 || Header.RequiredSignatures > Accounts.Length ||
            Header.ReadOnlySignedAccounts >= Header.RequiredSignatures ||
            Header.ReadOnlyUnsignedAccounts > Accounts.Length - Header.RequiredSignatures)
            throw new InvalidOperationException("Invalid message header or account count.");
        var size = (Version.HasValue ? 1 : 0) + 3 + LengthSize(Accounts.Length) + Accounts.Length * 32 + 32 + LengthSize(Instructions.Length);
        foreach (ref readonly var instruction in Instructions.AsSpan())
            size += 1 + LengthSize(instruction.KeyIndices.Length) + instruction.KeyIndices.Length + LengthSize(instruction.Data.Length) + instruction.Data.Length;
        if (Version.HasValue)
        {
            size += LengthSize(AddressTableLookups?.Length ?? 0);
            foreach (ref readonly var lookup in AddressTableLookups.AsSpan())
                size += 32 + LengthSize(lookup.WritableIndices.Length) + lookup.WritableIndices.Length + LengthSize(lookup.ReadOnlyIndices.Length) + lookup.ReadOnlyIndices.Length;
        }
        return size;
    }

    public void Serialize(Span<byte> buffer)
    {
        var size = Size();
        if (buffer.Length < size) throw new ArgumentException("Message buffer is too small.", nameof(buffer));
        SerializeCore(buffer);
    }

    private void SerializeCore(Span<byte> buffer)
    {
        var serializer = new BorshSerializer(buffer);
        if (Version.HasValue) serializer.Write((byte)(0x80 | Version.Value));
        serializer.Write(Header.RequiredSignatures);
        serializer.Write(Header.ReadOnlySignedAccounts);
        serializer.Write(Header.ReadOnlyUnsignedAccounts);
        WriteLength(ref serializer, Accounts.Length);
        foreach (var account in Accounts) serializer.Write(account.KeySpan);
        serializer.Write(RecentBlockHash.KeySpan);
        WriteLength(ref serializer, Instructions.Length);
        foreach (ref readonly var instruction in Instructions.AsSpan())
        {
            serializer.Write(instruction.ProgramIdIndex);
            WriteLength(ref serializer, instruction.KeyIndices.Length);
            serializer.Write(instruction.KeyIndices);
            WriteLength(ref serializer, instruction.Data.Length);
            serializer.Write(instruction.Data);
        }
        if (!Version.HasValue) return;
        WriteLength(ref serializer, AddressTableLookups?.Length ?? 0);
        foreach (ref readonly var lookup in AddressTableLookups.AsSpan())
        {
            serializer.Write(lookup.AccountKey.KeySpan);
            WriteLength(ref serializer, lookup.WritableIndices.Length);
            serializer.Write(lookup.WritableIndices);
            WriteLength(ref serializer, lookup.ReadOnlyIndices.Length);
            serializer.Write(lookup.ReadOnlyIndices);
        }
    }

    private static int LengthSize(int value)
    {
        if ((uint)value > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
        return value < 128 ? 1 : value < 16384 ? 2 : 3;
    }

    private static void WriteLength(ref BorshSerializer serializer, int value)
    {
        do
        {
            var next = value >> 7;
            serializer.Write((byte)((value & 0x7f) | (next == 0 ? 0 : 0x80)));
            value = next;
        } while (value != 0);
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct CompiledMessageHeader
{
    public byte RequiredSignatures;
    public byte ReadOnlySignedAccounts;
    public byte ReadOnlyUnsignedAccounts;
}
