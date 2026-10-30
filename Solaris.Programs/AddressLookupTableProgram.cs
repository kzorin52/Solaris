using System.Buffers.Binary;
using Solaris.Base.Account;
using Solaris.Transactions.Models;

namespace Solaris.Programs;

public static class AddressLookupTableProgram
{
    public static readonly PublicKey ProgramId = "AddressLookupTab1e1111111111111111111111111";

    public static TransactionInstruction CreateLookupTable(PublicKey authority, PublicKey payer, ulong recentSlot, out PublicKey address)
    {
        Span<byte> slot = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64LittleEndian(slot, recentSlot);
        var (key, bump) = PublicKey.FindProgramAddress(ProgramId, authority.KeySpan, slot);
        address = key ?? throw new InvalidOperationException("Unable to derive lookup table address.");
        var data = new byte[13];
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), recentSlot);
        data[12] = bump;
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true), AccountMeta.Writable(payer, true), AccountMeta.ReadOnly(SystemProgram.ProgramId)],
            Data = data
        };
    }

    public static TransactionInstruction FreezeLookupTable(PublicKey address, PublicKey authority) => AuthorityInstruction(1, address, authority);

    public static TransactionInstruction ExtendLookupTable(PublicKey address, PublicKey authority, ReadOnlySpan<PublicKey> addresses, PublicKey? payer = null)
    {
        if (addresses.IsEmpty || addresses.Length > 256) throw new ArgumentOutOfRangeException(nameof(addresses));
        var data = new byte[12 + addresses.Length * 32];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 2);
        BinaryPrimitives.WriteUInt64LittleEndian(data.AsSpan(4), (ulong)addresses.Length);
        for (var i = 0; i < addresses.Length; i++) addresses[i].KeySpan.CopyTo(data.AsSpan(12 + i * 32));
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = payer is null
                ? [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true)]
                : [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true), AccountMeta.Writable(payer, true), AccountMeta.ReadOnly(SystemProgram.ProgramId)],
            Data = data
        };
    }

    public static TransactionInstruction DeactivateLookupTable(PublicKey address, PublicKey authority) => AuthorityInstruction(3, address, authority);

    public static TransactionInstruction CloseLookupTable(PublicKey address, PublicKey authority, PublicKey recipient)
    {
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, 4);
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true), AccountMeta.Writable(recipient)],
            Data = data
        };
    }

    private static TransactionInstruction AuthorityInstruction(uint discriminator, PublicKey address, PublicKey authority)
    {
        var data = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(data, discriminator);
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true)],
            Data = data
        };
    }
}
