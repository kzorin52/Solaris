using Solaris.Base.Account;
using Solaris.Borsh;
using Solaris.Transactions.Models;

namespace Solaris.Programs.ALT;

public class AddressLookupTableState : IAccountData, IBorshDeserializable<AddressLookupTableState>
{
    public const int MetadataSize = 56;

    public PublicKey? PublicKey { get; set; }
    public ulong DeactivationSlot;
    public ulong LastExtendedSlot;
    public byte LastExtendedSlotStartIndex;
    public PublicKey? Authority;
    public PublicKey[] Addresses = [];

    public bool IsActive => DeactivationSlot == ulong.MaxValue;

    public static AddressLookupTableState Deserialize(ref BorshDeserializer des)
    {
        var length = des.Remaining;
        if (length < MetadataSize || (length - MetadataSize) % 32 != 0 ||
            (length - MetadataSize) / 32 > 256 || des.Integer<uint>() != 1)
            throw new ArgumentException("Invalid address lookup table account data.", nameof(des));

        var state = new AddressLookupTableState
        {
            DeactivationSlot = des.Integer<ulong>(),
            LastExtendedSlot = des.Integer<ulong>(),
            LastExtendedSlotStartIndex = des.Byte()
        };
        var authorityOption = des.Byte();
        if (authorityOption > 1)
            throw new ArgumentException("Invalid address lookup table authority option.", nameof(des));
        if (authorityOption == 1) state.Authority = des.PublicKey();
        else des.Skip(32);
        des.Skip(2);

        var count = (length - MetadataSize) / 32;
        var addresses = count == 0 ? [] : new PublicKey[count];
        for (var i = 0; i < addresses.Length; i++) addresses[i] = des.PublicKey();
        state.Addresses = addresses;
        return state;
    }

    public AddressLookupTableAccount ToAccount()
    {
        if (PublicKey is null) throw new InvalidOperationException("The lookup table account public key is required.");
        return new AddressLookupTableAccount(PublicKey, Addresses);
    }
}
