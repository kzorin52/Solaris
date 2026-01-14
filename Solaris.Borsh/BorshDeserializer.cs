using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Solaris.Base.Account;

namespace Solaris.Borsh;

public ref struct BorshDeserializer(ReadOnlySpan<byte> data)
{
    public int Offset;
    private readonly ReadOnlySpan<byte> _data = data;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe T Integer<T>() where T : unmanaged
    {
        var size = sizeof(T);

        var num = MemoryMarshal.Read<T>(_data.Slice(Offset, size));
        Offset += size;

        return num;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<byte> Span(int size)
    {
        var span = _data.Slice(Offset, size);
        Offset += size;

        return span;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Skip(int size)
    {
        Offset += size;
    }

    public PublicKey PublicKey() => Span(32).ToArray();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte Byte()
    {
        return _data[Offset++];
    }
}

public static class BorshDeserializationExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe T Integer<T>(this ReadOnlySpan<byte> data) where T : unmanaged
    {
        return MemoryMarshal.Read<T>(data[..sizeof(T)]);
    }
}