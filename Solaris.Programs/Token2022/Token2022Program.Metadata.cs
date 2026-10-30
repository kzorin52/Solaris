using System.Text;
using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.Token2022;

public static partial class Token2022Program
{
    public static TransactionInstruction InitializeTokenMetadata(PublicKey metadata, PublicKey updateAuthority,
        PublicKey mint, PublicKey mintAuthority, string name, string symbol, string uri)
    {
        var nameSize = Encoding.UTF8.GetByteCount(name);
        var symbolSize = Encoding.UTF8.GetByteCount(symbol);
        var uriSize = Encoding.UTF8.GetByteCount(uri);
        var data = new byte[checked(20 + nameSize + symbolSize + uriSize)];
        var serializer = new BorshSerializer(data);
        serializer.Write([210, 225, 30, 162, 88, 184, 77, 141]);
        WriteMetadataString(ref serializer, name, nameSize);
        WriteMetadataString(ref serializer, symbol, symbolSize);
        WriteMetadataString(ref serializer, uri, uriSize);
        return Instruction(data,
        [
            AccountMeta.Writable(metadata),
            AccountMeta.ReadOnly(updateAuthority),
            AccountMeta.ReadOnly(mint),
            AccountMeta.ReadOnly(mintAuthority, true)
        ]);
    }

    public static TransactionInstruction UpdateTokenMetadataField(PublicKey metadata, PublicKey updateAuthority,
        Token2022MetadataField field, string value)
    {
        var keySize = field.Kind == 3 ? Encoding.UTF8.GetByteCount(field.KeyValue!) : 0;
        var valueSize = Encoding.UTF8.GetByteCount(value);
        var data = new byte[checked(13 + valueSize + (field.Kind == 3 ? 4 + keySize : 0))];
        var serializer = new BorshSerializer(data);
        serializer.Write([221, 233, 49, 45, 181, 202, 220, 200]);
        serializer.Write(field.Kind);
        if (field.Kind == 3)
            WriteMetadataString(ref serializer, field.KeyValue!, keySize);
        WriteMetadataString(ref serializer, value, valueSize);
        return Instruction(data,
        [
            AccountMeta.Writable(metadata),
            AccountMeta.ReadOnly(updateAuthority, true)
        ]);
    }

    public static TransactionInstruction RemoveTokenMetadataKey(PublicKey metadata, PublicKey updateAuthority,
        string key, bool idempotent = false)
    {
        var keySize = Encoding.UTF8.GetByteCount(key);
        var data = new byte[checked(13 + keySize)];
        var serializer = new BorshSerializer(data);
        serializer.Write([234, 18, 32, 56, 89, 141, 37, 181]);
        serializer.Write(idempotent);
        WriteMetadataString(ref serializer, key, keySize);
        return Instruction(data,
        [
            AccountMeta.Writable(metadata),
            AccountMeta.ReadOnly(updateAuthority, true)
        ]);
    }

    public static TransactionInstruction UpdateTokenMetadataUpdateAuthority(PublicKey metadata,
        PublicKey updateAuthority, PublicKey? newUpdateAuthority)
    {
        var data = new FluentSerializer(40)
            .Write([215, 228, 166, 228, 84, 100, 86, 123])
            .Write(newUpdateAuthority ?? SystemProgram.ProgramId)
            .Build();
        return Instruction(data,
        [
            AccountMeta.Writable(metadata),
            AccountMeta.ReadOnly(updateAuthority, true)
        ]);
    }

    public static TransactionInstruction EmitTokenMetadata(PublicKey metadata, ulong? start = null, ulong? end = null)
    {
        var data = new byte[10 + (start.HasValue ? 8 : 0) + (end.HasValue ? 8 : 0)];
        var serializer = new BorshSerializer(data);
        serializer.Write([250, 166, 180, 250, 13, 12, 184, 70]);
        serializer.Write(start.HasValue);
        if (start.HasValue)
            serializer.WriteInteger(start.Value);
        serializer.Write(end.HasValue);
        if (end.HasValue)
            serializer.WriteInteger(end.Value);
        return Instruction(data, [AccountMeta.ReadOnly(metadata)]);
    }

    private static void WriteMetadataString(ref BorshSerializer serializer, string value, int byteCount)
    {
        serializer.WriteInteger((uint)byteCount);
        Encoding.UTF8.GetBytes(value.AsSpan(), serializer.Buffer.Slice(serializer.Offset, byteCount));
        serializer.Offset += byteCount;
    }
}

public readonly struct Token2022MetadataField
{
    internal byte Kind { get; }
    internal string? KeyValue { get; }

    private Token2022MetadataField(byte kind, string? key = null)
    {
        Kind = kind;
        KeyValue = key;
    }

    public static Token2022MetadataField Name => new(0);
    public static Token2022MetadataField Symbol => new(1);
    public static Token2022MetadataField Uri => new(2);

    public static Token2022MetadataField Key(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new Token2022MetadataField(3, key);
    }
}
