using System.Buffers.Binary;
using Solaris.Base.Account;
using Solaris.Transactions.Models;

namespace Solaris.Programs;

public readonly ref struct AddressLookupTableState
{
    public const int MetadataSize = 56;
    private readonly ReadOnlySpan<byte> _data;

    public AddressLookupTableState(ReadOnlySpan<byte> data)
    {
        if (data.Length < MetadataSize || (data.Length - MetadataSize) % 32 != 0 ||
            (data.Length - MetadataSize) / 32 > 256 || BinaryPrimitives.ReadUInt32LittleEndian(data) != 1 || data[21] > 1)
            throw new ArgumentException("Invalid address lookup table account data.", nameof(data));
        _data = data;
    }

    public ulong DeactivationSlot => BinaryPrimitives.ReadUInt64LittleEndian(_data[4..]);
    public ulong LastExtendedSlot => BinaryPrimitives.ReadUInt64LittleEndian(_data[12..]);
    public byte LastExtendedSlotStartIndex => _data[20];
    public bool IsActive => DeactivationSlot == ulong.MaxValue;
    public PublicKeyValue? Authority => _data[21] == 0 ? null : PublicKeyValue.Create(_data[22..54]);
    public int AddressCount => (_data.Length - MetadataSize) / 32;
    public ReadOnlySpan<byte> AddressBytes => _data[MetadataSize..];

    public PublicKeyValue GetAddress(int index)
    {
        if ((uint)index >= (uint)AddressCount) throw new ArgumentOutOfRangeException(nameof(index));
        return PublicKeyValue.Create(_data.Slice(MetadataSize + index * 32, 32));
    }

    public AddressLookupTableAccount ToAccount(PublicKey key)
    {
        var addresses = new PublicKey[AddressCount];
        for (var i = 0; i < addresses.Length; i++) addresses[i] = new PublicKey(AddressBytes.Slice(i * 32, 32));
        return new AddressLookupTableAccount(key, addresses);
    }
}
