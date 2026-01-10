using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Transactions.LowLevel;

/// <summary>
///     Represents a compiled transaction message ready to be signed and submitted to the network.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CompiledMessage
{
    /// <summary>
    ///     The message header containing signature counts and account privileges.
    /// </summary>
    public CompiledMessageHeader Header;

    /// <summary>
    ///     The list of public keys (accounts) referenced by this transaction.
    /// </summary>
    public PublicKey[] Accounts;

    /// <summary>
    ///     The hash of a recent block, required for transaction validity.
    /// </summary>
    public PublicKey RecentBlockHash;

    /// <summary>
    ///     The instructions included in this transaction.
    /// </summary>
    public CompiledInstruction[] Instructions;

    /// <summary>
    ///     Builds the final transaction packet by serializing the message and appending signatures.
    /// </summary>
    /// <param name="keys">The private keys used to sign the transaction.</param>
    /// <returns>The complete binary representation of the signed transaction.</returns>
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

    /// <summary>
    ///     Calculates the total size in bytes required to serialize this message.
    /// </summary>
    /// <returns>The size in bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Size()
    {
        // 3 bytes for Header
        // 1 byte for Accounts count
        // 32 bytes per Account
        // 32 bytes for RecentBlockHash
        // 1 byte for Instructions count
        var bufferLen = 3 + 1 + Accounts.Length * 32 + 32 + 1;
        
        foreach (ref var instruction in Instructions.AsSpan())
            // For each instruction:
            // Data length + KeyIndices length + 1 byte (ProgramIdIndex) + 2 bytes (KeyIndices count + Data count)
            bufferLen += instruction.Data.Length + instruction.KeyIndices.Length + 1 + 2;
        return bufferLen;
    }

    /// <summary>
    ///     Serializes the message into the provided buffer using Borsh serialization.
    /// </summary>
    /// <param name="buffer">The target buffer to write to.</param>
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

/// <summary>
///     Header of the compiled message defining account signature requirements.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct CompiledMessageHeader
{
    /// <summary>
    ///     The number of signatures required for this message to be considered valid.
    /// </summary>
    public byte RequiredSignatures;

    /// <summary>
    ///     The number of read-only accounts that must also sign the transaction.
    /// </summary>
    public byte ReadOnlySignedAccounts;

    /// <summary>
    ///     The number of read-only accounts that do not need to sign.
    /// </summary>
    public byte ReadOnlyUnsignedAccounts;
}