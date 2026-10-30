using Solaris.Base.Account;
using Solaris.Borsh;

namespace Solaris.Programs.Token2022;

public readonly record struct TransferHookExtraAccountMeta(byte Discriminator, PublicKeyValue AddressConfig, bool IsSigner, bool IsWritable);

public sealed class TransferHookExtraAccountMetaList : IAccountData, IBorshDeserializable<TransferHookExtraAccountMetaList>
{
    internal static ReadOnlySpan<byte> ExecuteDiscriminator => [105, 37, 101, 197, 75, 251, 102, 26];

    public PublicKey? PublicKey { get; set; }
    public TransferHookExtraAccountMeta[] Accounts { get; set; } = [];

    public static TransferHookExtraAccountMetaList Deserialize(ref BorshDeserializer des)
    {
        while (des.Remaining >= 12)
        {
            var discriminator = des.Span(8);
            var length = des.Integer<uint>();
            if (length > des.Remaining) throw new ArgumentException("Truncated transfer hook validation data.", nameof(des));
            var value = des.Span((int)length);
            if (!discriminator.SequenceEqual(ExecuteDiscriminator)) continue;
            var entries = new BorshDeserializer(value);
            if (entries.Remaining < 4) throw new ArgumentException("Missing transfer hook account count.", nameof(des));
            var count = entries.Integer<uint>();
            if ((ulong)count * 35 != (ulong)entries.Remaining)
                throw new ArgumentException("Invalid transfer hook account count.", nameof(des));
            var accounts = count == 0 ? [] : new TransferHookExtraAccountMeta[(int)count];
            for (var i = 0; i < accounts.Length; i++)
            {
                var kind = entries.Byte();
                if (kind is > 2 and < 128) throw new ArgumentException("Invalid transfer hook address discriminator.", nameof(des));
                var addressConfig = PublicKeyValue.Create(entries.Span(32));
                var isSigner = entries.Byte();
                var isWritable = entries.Byte();
                if (isSigner > 1 || isWritable > 1) throw new ArgumentException("Invalid transfer hook account flags.", nameof(des));
                accounts[i] = new TransferHookExtraAccountMeta(kind, addressConfig, isSigner != 0, isWritable != 0);
            }
            return new TransferHookExtraAccountMetaList { Accounts = accounts };
        }
        throw new ArgumentException("Transfer hook Execute account metadata was not found.", nameof(des));
    }
}
