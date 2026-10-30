using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.ALT;

public static class AddressLookupTableProgram
{
    public static readonly PublicKey ProgramId = "AddressLookupTab1e1111111111111111111111111";

    public static TransactionInstruction CreateLookupTable(PublicKey authority, PublicKey payer, ulong recentSlot, out PublicKey address)
    {
        var serializer = new FluentSerializer(13);
        serializer.WriteInteger(0u).WriteInteger(recentSlot);
        var data = serializer.Build();
        var (key, bump) = PublicKey.FindProgramAddress(ProgramId, authority.KeySpan, data.AsSpan(4, 8));
        address = key ?? throw new InvalidOperationException("Unable to derive lookup table address.");
        serializer.Write(bump);
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
        var serializer = new FluentSerializer(12 + addresses.Length * 32);
        serializer.WriteInteger(2u).WriteInteger((ulong)addresses.Length);
        foreach (var key in addresses) serializer.Write(key.KeySpan);
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = payer is null
                ? [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true)]
                : [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true), AccountMeta.Writable(payer, true), AccountMeta.ReadOnly(SystemProgram.ProgramId)],
            Data = serializer.Build()
        };
    }

    public static TransactionInstruction DeactivateLookupTable(PublicKey address, PublicKey authority) => AuthorityInstruction(3, address, authority);

    public static TransactionInstruction CloseLookupTable(PublicKey address, PublicKey authority, PublicKey recipient)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true), AccountMeta.Writable(recipient)],
            Data = new FluentSerializer(4).WriteInteger(4u).Build()
        };
    }

    private static TransactionInstruction AuthorityInstruction(uint discriminator, PublicKey address, PublicKey authority)
    {
        return new TransactionInstruction
        {
            ProgramId = ProgramId,
            Keys = [AccountMeta.Writable(address), AccountMeta.ReadOnly(authority, true)],
            Data = new FluentSerializer(4).WriteInteger(discriminator).Build()
        };
    }
}
