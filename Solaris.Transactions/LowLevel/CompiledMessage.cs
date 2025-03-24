using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Transactions.LowLevel;

[StructLayout(LayoutKind.Sequential)]
public struct CompiledMessage
{
    public CompiledMessageHeader Header;
    public PublicKey[] Accounts;
    public PublicKey RecentBlockHash;
    public CompiledInstruction[] Instructions;

    public byte[] BuildTransaction(PrivateKey[] keys)
    {
        var signaturesLen = 64 * keys.Length;
        var size = Size();

        var buffer = new byte[signaturesLen + size + 1];
        var span = buffer.AsSpan();
        span[0] = (byte)keys.Length;

        var signatures = span.Slice(1, signaturesLen);
        var ser = span.Slice(signaturesLen + 1, size);

        Serialize(ser);
        for (var i = 0; i < keys.Length; i++)
            keys[i].Sign(ser, signatures.Slice(i * 64, 64));

        return buffer;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Size()
    {
        var bufferLen = 3 + 1 + Accounts.Length * 32 + 32 + 1;
        foreach (ref var instruction in Instructions.AsSpan())
            bufferLen += instruction.Data.Length + instruction.KeyIndices.Length + 1 + 2;
        return bufferLen;
    }

    public void Serialize(Span<byte> buffer)
    {
        var serializer = new BorshSerializer(buffer);
        serializer.Write(ref Header);
        serializer.Write((byte)Accounts.Length);
        foreach (var account in Accounts) serializer.Write(account.KeyMemory.Span);

        serializer.Write(RecentBlockHash.KeyMemory.Span);

        serializer.Write((byte)Instructions.Length);
        foreach (ref var instruction in Instructions.AsSpan())
        {
            serializer.Write(instruction.ProgramIdIndex);
            serializer.Write((byte)instruction.KeyIndices.Length);
            serializer.Write(instruction.KeyIndices);
            serializer.Write((byte)instruction.Data.Length);
            serializer.Write(instruction.Data);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct CompiledMessageHeader
{
    public byte RequiredSignatures;
    public byte ReadOnlySignedAccounts;
    public byte ReadOnlyUnsignedAccounts;
}